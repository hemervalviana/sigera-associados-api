using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Identity;

namespace SigeraAssociados.Infrastructure.Acesso;

/// <summary>
/// Emite, renova (com rotação) e revoga tokens de renovação,
/// gravados como hash na tabela TokensRenovacao do banco de identidade.
/// </summary>
public sealed class TokensRenovacaoService : ITokensRenovacaoService
{
	private const int BytesDoToken = 64;

	private readonly SigeraIdentityDbContext _identityDb;
	private readonly TimeProvider _relogio;
	private readonly OpcoesSessao _opcoes;

	public TokensRenovacaoService(
		SigeraIdentityDbContext identityDb,
		TimeProvider relogio,
		IOptions<OpcoesSessao> opcoes)
	{
		_identityDb = identityDb;
		_relogio = relogio;
		_opcoes = opcoes.Value;
	}

	/// <summary>
	/// SHA-256 do token em hexadecimal.
	/// </summary>
	internal static string Hash(string token) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

	private DateTime AgoraUtc => _relogio.GetUtcNow().UtcDateTime;

	/// <inheritdoc />
	public async Task<TokenRenovacaoEmitido> EmitirAsync(
		string usuarioId,
		CancellationToken cancellationToken = default)
	{
		var (emitido, _) = Novo(usuarioId);

		await _identityDb.SaveChangesAsync(cancellationToken);

		return emitido;
	}

	/// <inheritdoc />
	public async Task<ResultadoRenovacao> RenovarAsync(
		string token,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			return new ResultadoRenovacao(StatusRenovacao.Invalido);
		}

		var hash = Hash(token);

		var registro = await _identityDb.TokensRenovacao
			.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

		if (registro is null)
		{
			return new ResultadoRenovacao(StatusRenovacao.Invalido);
		}

		if (registro.RevogadoEmUtc is not null)
		{
			// Token já trocado sendo usado de novo: alguém pode ter copiado
			// o token. Encerra todas as sessões do usuário.
			if (registro.MotivoRevogacao == MotivosRevogacao.Renovado)
			{
				await RevogarTodosAsync(
					registro.UsuarioId,
					MotivosRevogacao.ReusoDetectado,
					cancellationToken);

				return new ResultadoRenovacao(
					StatusRenovacao.Reutilizado,
					registro.UsuarioId);
			}

			return new ResultadoRenovacao(StatusRenovacao.Invalido, registro.UsuarioId);
		}

		if (registro.ExpiraEmUtc <= AgoraUtc)
		{
			return new ResultadoRenovacao(StatusRenovacao.Expirado, registro.UsuarioId);
		}

		var agora = _relogio.GetUtcNow();

		var usuarioDisponivel = await _identityDb.Users
			.AnyAsync(
				u => u.Id == registro.UsuarioId
					&& u.EmailConfirmed
					&& (u.LockoutEnd == null || u.LockoutEnd <= agora),
				cancellationToken);

		if (!usuarioDisponivel)
		{
			return new ResultadoRenovacao(
				StatusRenovacao.UsuarioIndisponivel,
				registro.UsuarioId);
		}

		var (novo, novoHash) = Novo(registro.UsuarioId);

		registro.RevogadoEmUtc = AgoraUtc;
		registro.MotivoRevogacao = MotivosRevogacao.Renovado;
		registro.SubstituidoPorHash = novoHash;

		await _identityDb.SaveChangesAsync(cancellationToken);

		return new ResultadoRenovacao(StatusRenovacao.Sucesso, registro.UsuarioId, novo);
	}

	/// <inheritdoc />
	public async Task<bool> RevogarAsync(
		string token,
		string usuarioId,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			return false;
		}

		var hash = Hash(token);

		var registro = await _identityDb.TokensRenovacao
			.FirstOrDefaultAsync(
				t => t.TokenHash == hash && t.UsuarioId == usuarioId,
				cancellationToken);

		if (registro is null || registro.RevogadoEmUtc is not null)
		{
			return false;
		}

		registro.RevogadoEmUtc = AgoraUtc;
		registro.MotivoRevogacao = MotivosRevogacao.Logout;

		await _identityDb.SaveChangesAsync(cancellationToken);

		return true;
	}

	/// <inheritdoc />
	public async Task<int> RevogarTodosAsync(
		string usuarioId,
		string motivo,
		CancellationToken cancellationToken = default)
	{
		var ativos = await _identityDb.TokensRenovacao
			.Where(t => t.UsuarioId == usuarioId && t.RevogadoEmUtc == null)
			.ToListAsync(cancellationToken);

		foreach (var token in ativos)
		{
			token.RevogadoEmUtc = AgoraUtc;
			token.MotivoRevogacao = motivo;
		}

		if (ativos.Count > 0)
		{
			await _identityDb.SaveChangesAsync(cancellationToken);
		}

		return ativos.Count;
	}

	/// <summary>
	/// Gera um token aleatório e adiciona o registro (sem salvar).
	/// </summary>
	private (TokenRenovacaoEmitido Emitido, string Hash) Novo(string usuarioId)
	{
		var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(BytesDoToken));
		var hash = Hash(token);
		var criadoEm = AgoraUtc;
		var expiraEm = criadoEm.AddDays(_opcoes.RefreshTokenDias);

		_identityDb.TokensRenovacao.Add(new TokenRenovacao
		{
			UsuarioId = usuarioId,
			TokenHash = hash,
			CriadoEmUtc = criadoEm,
			ExpiraEmUtc = expiraEm
		});

		return (
			new TokenRenovacaoEmitido(token, new DateTimeOffset(expiraEm, TimeSpan.Zero)),
			hash);
	}
}

/// <summary>
/// Motivos gravados em TokenRenovacao.MotivoRevogacao.
/// </summary>
public static class MotivosRevogacao
{
	public const string Renovado = "renovado";
	public const string Logout = "logout";
	public const string LogoutTodos = "logout_todos";
	public const string Bloqueio = "bloqueio";
	public const string SenhaRedefinida = "senha_redefinida";
	public const string ReusoDetectado = "reuso_detectado";
}
