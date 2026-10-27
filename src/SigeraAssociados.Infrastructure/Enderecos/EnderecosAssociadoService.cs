using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Enderecos;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Enderecos;
using SigeraAssociados.Application.Pessoas;
using SigeraAssociados.Infrastructure.Pessoas;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;
using static SigeraAssociados.Infrastructure.Persistence.Legado;

namespace SigeraAssociados.Infrastructure.Enderecos;

/// <summary>
/// Endereços do associado (T000ENDE). SQ_EN_PES = MAX + 1 da pessoa, com
/// UPDLOCK, HOLDLOCK. Mantém um único endereço principal (ID_EN_PCP = S).
/// </summary>
public sealed class EnderecosAssociadoService : IEnderecosAssociadoService
{
	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _sistema;
	private readonly IEscopoAcessoService _escopoAcesso;

	public EnderecosAssociadoService(
		SigeraDbContextFactory factory,
		SigeraSystemContext sistema,
		IEscopoAcessoService escopoAcesso)
	{
		_factory = factory;
		_sistema = sistema;
		_escopoAcesso = escopoAcesso;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<EnderecoAssociadoDto>?> ListarAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return null;
		}

		return (await Projetar(db, emp, sqPesCli).ToListAsync(cancellationToken))
			.Select(ParaDto)
			.ToList();
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<EnderecoAssociadoDto>> IncluirAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		EnderecoAssociadoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoEnderecoAssociado.Validar(dados) is string erro)
		{
			return Invalido<EnderecoAssociadoDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<EnderecoAssociadoDto>(AcessoTitular.NaoEncontrado(titular));
		}

		if (await ValidarReferenciasAsync(db, emp, dados, cancellationToken) is string erroReferencia)
		{
			return Invalido<EnderecoAssociadoDto>(erroReferencia);
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var sqEndereco = await NumeroAsync(db,
			$"SELECT ISNULL(MAX(SQ_EN_PES), 0) + 1 AS [Value] FROM dbo.T000ENDE WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp} AND SQ_PES = {sqPesCli}",
			cancellationToken);

		if (sqEndereco > ValidacaoEnderecoAssociado.MaximoEnderecos)
		{
			return Invalido<EnderecoAssociadoDto>("O associado atingiu o limite de 99 endereços.");
		}

		var outros = await db.Enderecos
			.Where(e => e.CD_EMP == emp && e.SQ_PES == sqPesCli)
			.ToListAsync(cancellationToken);

		// O primeiro endereço é sempre o principal.
		var principal = outros.Count == 0 || dados.Principal == true;

		var endereco = new T000ENDE { CD_EMP = emp, SQ_PES = sqPesCli, SQ_EN_PES = sqEndereco };
		Aplicar(dados, endereco, principal);

		if (principal)
		{
			DesmarcarPrincipal(outros);
		}

		db.Enderecos.Add(endereco);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<EnderecoAssociadoDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Endereço incluído.", await ObterAsync(db, emp, sqPesCli, sqEndereco, cancellationToken));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<EnderecoAssociadoDto>> AlterarAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		decimal sqEndereco,
		EnderecoAssociadoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoEnderecoAssociado.Validar(dados) is string erro)
		{
			return Invalido<EnderecoAssociadoDto>(erro);
		}

		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<EnderecoAssociadoDto>(AcessoTitular.NaoEncontrado(titular));
		}

		var enderecos = await db.Enderecos
			.Where(e => e.CD_EMP == emp && e.SQ_PES == sqPesCli)
			.ToListAsync(cancellationToken);

		var endereco = enderecos.FirstOrDefault(e => e.SQ_EN_PES == sqEndereco);

		if (endereco is null)
		{
			return NaoEncontrado<EnderecoAssociadoDto>("Endereço não encontrado.");
		}

		if (await ValidarReferenciasAsync(db, emp, dados, cancellationToken) is string erroReferencia)
		{
			return Invalido<EnderecoAssociadoDto>(erroReferencia);
		}

		var eraPrincipal = endereco.ID_EN_PCP == ValidacaoEnderecoAssociado.Principal;
		var outros = enderecos.Where(e => e.SQ_EN_PES != sqEndereco).ToList();

		// Principal omitido: mantém como estava. Não dá para tirar o principal
		// do único endereço nem deixar o associado sem principal.
		var principal = dados.Principal ?? eraPrincipal;

		if (!principal && eraPrincipal)
		{
			if (outros.Count == 0)
			{
				return Invalido<EnderecoAssociadoDto>(
					"O único endereço do associado precisa ser o principal.");
			}

			return Invalido<EnderecoAssociadoDto>(
				"Para trocar o endereço principal, marque outro endereço como principal.");
		}

		Aplicar(dados, endereco, principal);

		if (principal)
		{
			DesmarcarPrincipal(outros);
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Invalido<EnderecoAssociadoDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Endereço alterado.", await ObterAsync(db, emp, sqPesCli, sqEndereco, cancellationToken));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<EnderecoAssociadoDto>> ExcluirAsync(
		TitularCadastro titular,
		decimal sqPesCli,
		decimal sqEndereco,
		CancellationToken cancellationToken = default)
	{
		await using var db = CriarContexto();

		if (await AbrirTitularAsync(db, titular, sqPesCli, cancellationToken) is not decimal emp)
		{
			return NaoEncontrado<EnderecoAssociadoDto>(AcessoTitular.NaoEncontrado(titular));
		}

		var dto = await ObterAsync(db, emp, sqPesCli, sqEndereco, cancellationToken);

		var enderecos = await db.Enderecos
			.Where(e => e.CD_EMP == emp && e.SQ_PES == sqPesCli)
			.ToListAsync(cancellationToken);

		var endereco = enderecos.FirstOrDefault(e => e.SQ_EN_PES == sqEndereco);

		if (endereco is null)
		{
			return NaoEncontrado<EnderecoAssociadoDto>("Endereço não encontrado.");
		}

		db.Enderecos.Remove(endereco);

		// Era o principal: o de menor sequência que sobrar assume.
		if (endereco.ID_EN_PCP == ValidacaoEnderecoAssociado.Principal)
		{
			var novoPrincipal = enderecos
				.Where(e => e.SQ_EN_PES != sqEndereco)
				.OrderBy(e => e.SQ_EN_PES)
				.FirstOrDefault();

			if (novoPrincipal is not null)
			{
				novoPrincipal.ID_EN_PCP = ValidacaoEnderecoAssociado.Principal;
			}
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<EnderecoAssociadoDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Endereço excluído.", dto);
	}

	// ----- Apoio -----

	private SigeraDbContext CriarContexto()
	{
		var sistema = _sistema.SystemCode
			?? throw new InvalidOperationException("O sistema SIGERA não foi selecionado para esta requisição.");

		return _factory.Create(sistema);
	}

	/// <summary>
	/// Empresa da base quando o titular (associado no escopo do usuário, ou
	/// corretor não excluído) existe; null para tratar como não encontrado.
	/// </summary>
	private Task<decimal?> AbrirTitularAsync(
		SigeraDbContext db,
		TitularCadastro titular,
		decimal sqPes,
		CancellationToken cancellationToken) =>
		AcessoTitular.AbrirAsync(db, _escopoAcesso, titular, sqPes, cancellationToken);

	private static async Task<string?> ValidarReferenciasAsync(
		SigeraDbContext db,
		decimal emp,
		EnderecoAssociadoDados dados,
		CancellationToken cancellationToken)
	{
		var tipo = dados.CdTipo!.Value;

		if (!await db.TiposEndereco.AnyAsync(t => t.CD_EMP == emp && t.CD_TP_EN == tipo, cancellationToken))
		{
			return $"O tipo de endereço {tipo:0} não está cadastrado (T000TEND).";
		}

		var municipio = dados.CdMunicipio!.Value;

		if (!await db.Municipios.AnyAsync(m => m.CD_EMP == emp && m.CD_MUN == municipio, cancellationToken))
		{
			return $"O município {municipio:0} não está cadastrado (T000MUNI).";
		}

		return null;
	}

	private static void DesmarcarPrincipal(IEnumerable<T000ENDE> enderecos)
	{
		foreach (var outro in enderecos.Where(e => e.ID_EN_PCP != ValidacaoEnderecoAssociado.NaoPrincipal))
		{
			outro.ID_EN_PCP = ValidacaoEnderecoAssociado.NaoPrincipal;
		}
	}

	private static void Aplicar(EnderecoAssociadoDados d, T000ENDE e, bool principal)
	{
		e.CD_TP_EN = d.CdTipo!.Value;
		e.NM_LOG = d.Logradouro!.Trim();
		e.NR_LOG = d.Numero;
		e.CP_EN = Texto(d.Complemento);
		e.NM_BAI = Texto(d.Bairro);
		e.NR_CEP = Numero(d.Cep);
		e.CD_MUN = d.CdMunicipio!.Value;
		e.ID_EN_PCP = principal ? ValidacaoEnderecoAssociado.Principal : ValidacaoEnderecoAssociado.NaoPrincipal;
	}

	/// <summary>
	/// Linha lida do banco, antes de formatar (o CEP é numérico e perde
	/// o zero à esquerda; é formatado com 8 dígitos depois da consulta).
	/// </summary>
	private sealed record LinhaEndereco(
		decimal SqEndereco,
		decimal CdTipo,
		string? Tipo,
		string Logradouro,
		decimal? Numero,
		string? Complemento,
		string? Bairro,
		decimal? Cep,
		decimal CdMunicipio,
		string? Municipio,
		string? Uf,
		bool Principal);

	/// <summary>
	/// Endereços da pessoa (ou só um, com sqEndereco). O filtro vem antes
	/// do Select: o Entity Framework não traduz filtro sobre o resultado
	/// montado por construtor.
	/// </summary>
	private static IQueryable<LinhaEndereco> Projetar(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal? sqEndereco = null)
	{
		var enderecos = db.Enderecos
			.AsNoTracking()
			.Where(e => e.CD_EMP == emp && e.SQ_PES == sqPesCli);

		if (sqEndereco is decimal sequencia)
		{
			enderecos = enderecos.Where(e => e.SQ_EN_PES == sequencia);
		}

		return enderecos
			.OrderByDescending(e => e.ID_EN_PCP == ValidacaoEnderecoAssociado.Principal)
			.ThenBy(e => e.SQ_EN_PES)
			.Select(e => new LinhaEndereco(
				e.SQ_EN_PES,
				e.CD_TP_EN,
				db.TiposEndereco.Where(t => t.CD_EMP == e.CD_EMP && t.CD_TP_EN == e.CD_TP_EN).Select(t => t.DE_TP_EN).FirstOrDefault(),
				e.NM_LOG,
				e.NR_LOG,
				e.CP_EN,
				e.NM_BAI,
				e.NR_CEP,
				e.CD_MUN,
				db.Municipios.Where(m => m.CD_EMP == e.CD_EMP && m.CD_MUN == e.CD_MUN).Select(m => m.DE_MUN).FirstOrDefault(),
				db.Municipios.Where(m => m.CD_EMP == e.CD_EMP && m.CD_MUN == e.CD_MUN).Select(m => m.SG_UF).FirstOrDefault(),
				e.ID_EN_PCP == ValidacaoEnderecoAssociado.Principal));
	}

	private static EnderecoAssociadoDto ParaDto(LinhaEndereco l) => new()
	{
		SqEndereco = l.SqEndereco,
		CdTipo = l.CdTipo,
		Tipo = l.Tipo,
		Logradouro = l.Logradouro,
		Numero = l.Numero,
		Complemento = l.Complemento,
		Bairro = l.Bairro,
		Cep = Digitos(l.Cep, 8),
		CdMunicipio = l.CdMunicipio,
		Municipio = l.Municipio,
		Uf = l.Uf,
		Principal = l.Principal
	};

	private static async Task<EnderecoAssociadoDto?> ObterAsync(
		SigeraDbContext db,
		decimal emp,
		decimal sqPesCli,
		decimal sqEndereco,
		CancellationToken cancellationToken)
	{
		var linha = await Projetar(db, emp, sqPesCli, sqEndereco).FirstOrDefaultAsync(cancellationToken);

		return linha is null ? null : ParaDto(linha);
	}
}
