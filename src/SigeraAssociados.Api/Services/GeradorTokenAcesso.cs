using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace SigeraAssociados.Api.Services;

/// <summary>
/// Gera o token de acesso (JWT) do usuário, com os grupos atuais
/// e o carimbo de segurança, conferido a cada requisição.
/// </summary>
public sealed class GeradorTokenAcesso
{
	/// <summary>
	/// Claim com o carimbo de segurança (SecurityStamp) do usuário.
	/// Se o carimbo mudar (bloqueio, troca de grupo, senha nova,
	/// logout de todos os dispositivos), o token deixa de valer.
	/// </summary>
	public const string ClaimCarimbo = "carimbo";

	private readonly IConfiguration _configuration;
	private readonly UserManager<IdentityUser> _userManager;
	private readonly TimeProvider _relogio;

	public GeradorTokenAcesso(
		IConfiguration configuration,
		UserManager<IdentityUser> userManager,
		TimeProvider relogio)
	{
		_configuration = configuration;
		_userManager = userManager;
		_relogio = relogio;
	}

	public sealed record TokenAcesso(string Token, int ExpiraEmSegundos, DateTimeOffset ExpiraEm);

	public async Task<TokenAcesso> GerarAsync(IdentityUser usuario)
	{
		var issuer = _configuration["Jwt:Issuer"]
			?? throw new InvalidOperationException("A configuração Jwt:Issuer não foi definida.");

		var audience = _configuration["Jwt:Audience"]
			?? throw new InvalidOperationException("A configuração Jwt:Audience não foi definida.");

		var signingKey = _configuration["Jwt:SigningKey"]
			?? throw new InvalidOperationException("A chave de assinatura JWT não foi configurada.");

		var minutos = _configuration.GetValue<int>("Jwt:AccessTokenMinutes");

		if (minutos is < 1 or > 1440)
		{
			throw new InvalidOperationException("Jwt:AccessTokenMinutes deve estar entre 1 e 1440 minutos.");
		}

		var keyBytes = Convert.FromBase64String(signingKey);

		if (keyBytes.Length < 32)
		{
			throw new InvalidOperationException("A chave JWT deve conter pelo menos 32 bytes.");
		}

		// Garante um carimbo de segurança para contas antigas sem ele.
		var carimbo = await _userManager.GetSecurityStampAsync(usuario);

		if (string.IsNullOrEmpty(carimbo))
		{
			await _userManager.UpdateSecurityStampAsync(usuario);
			carimbo = await _userManager.GetSecurityStampAsync(usuario);
		}

		var agora = _relogio.GetUtcNow().UtcDateTime;
		var expiracao = agora.AddMinutes(minutos);

		var claims = new List<Claim>
		{
			new(JwtRegisteredClaimNames.Sub, usuario.Id),
			new(ClaimTypes.NameIdentifier, usuario.Id),
			new(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
			new(ClaimTypes.Email, usuario.Email ?? string.Empty),
			new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
			new(ClaimCarimbo, carimbo)
		};

		foreach (var funcao in await _userManager.GetRolesAsync(usuario))
		{
			claims.Add(new Claim(ClaimTypes.Role, funcao));
		}

		var credenciais = new SigningCredentials(
			new SymmetricSecurityKey(keyBytes),
			SecurityAlgorithms.HmacSha256);

		var token = new JwtSecurityToken(
			issuer: issuer,
			audience: audience,
			claims: claims,
			notBefore: agora,
			expires: expiracao,
			signingCredentials: credenciais);

		return new TokenAcesso(
			new JwtSecurityTokenHandler().WriteToken(token),
			checked(minutos * 60),
			new DateTimeOffset(expiracao, TimeSpan.Zero));
	}
}
