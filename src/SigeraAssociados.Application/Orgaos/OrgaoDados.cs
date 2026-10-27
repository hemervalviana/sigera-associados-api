namespace SigeraAssociados.Application.Orgaos;

/// <summary>
/// Dados de inclusão e alteração de um órgão (T000ORGA).
/// Na alteração, o código vem da rota e CdOrg é ignorado.
/// </summary>
public sealed class OrgaoDados
{
	/// <summary>
	/// Código do órgão (CD_ORG, até 6 dígitos). Na inclusão, omitir para usar MAX + 1.
	/// </summary>
	public decimal? CdOrg { get; init; }

	/// <summary>
	/// Nome (NM_ORG, até 50). Obrigatório.
	/// </summary>
	/// <example>SECRETARIA DA EDUCAÇÃO</example>
	public string? Nome { get; init; }

	/// <summary>Sigla (SGL_ORG, até 100).</summary>
	/// <example>SEDUC</example>
	public string? Sigla { get; init; }

	/// <summary>Número do órgão (NR_ORG, até 7).</summary>
	public string? Numero { get; init; }

	/// <summary>Grupo (CD_GP), de GET /api/orgaos/grupos.</summary>
	public decimal? CdGrupo { get; init; }

	/// <summary>Subórgão (SQ_SBOR), de GET /api/orgaos/suborgaos.</summary>
	public decimal? SqSubOrgao { get; init; }

	/// <summary>UF (SG_UF, 2 letras).</summary>
	public string? Uf { get; init; }

	/// <summary>Filial (CD_FIL), de GET /api/cadastros/filiais.</summary>
	public decimal? CdFil { get; init; }

	/// <summary>
	/// Situação do órgão (CD_SIT_ORG): 1 = ATIVO, 0 = INATIVO.
	/// Omitida na inclusão: 1 (ATIVO).
	/// </summary>
	public decimal? CdSituacao { get; init; }

	/// <summary>
	/// Descrição da situação (ATIVO ou INATIVO). Só na resposta; ignorada ao gravar.
	/// </summary>
	public string? Situacao { get; init; }

	/// <summary>Data limite de processamento (DT_LIM_PRO).</summary>
	public DateTime? DataLimiteProcessamento { get; init; }

	/// <summary>Data de pagamento (DT_PAG).</summary>
	public DateTime? DataPagamento { get; init; }

	/// <summary>Endereço do órgão (NM_LOG_ORG, NR_LOG_ORG, CP_EN_ORG, NM_BAI_ORG, NR_CEP_ORG, CD_MUN_ORG).</summary>
	public EnderecoDados? Endereco { get; init; }

	/// <summary>Contato administrativo (NM_CON_ADM, NR_FN_ADM, NM_EML_ADM).</summary>
	public ContatoDados? ContatoAdministrativo { get; init; }

	/// <summary>Contato financeiro (NM_CON_FIN, NR_FN_FIN, NM_EML_FIN).</summary>
	public ContatoDados? ContatoFinanceiro { get; init; }

	/// <summary>Empresa processadora da folha (NM_EMP_PRO e campos _EP).</summary>
	public EmpresaProcessadoraDados? EmpresaProcessadora { get; init; }
}

/// <summary>
/// Endereço: logradouro e bairro até 100 e 50, complemento até 50,
/// número até 99999, CEP com 8 números, município de GET /api/cadastros/municipios.
/// </summary>
public sealed class EnderecoDados
{
	public string? Logradouro { get; init; }

	public decimal? Numero { get; init; }

	public string? Complemento { get; init; }

	public string? Bairro { get; init; }

	public string? Cep { get; init; }

	public decimal? CdMunicipio { get; init; }
}

/// <summary>
/// Contato: nome e e-mail até 100; telefone até 15 números (com DDD).
/// </summary>
public sealed class ContatoDados
{
	public string? Nome { get; init; }

	public string? Telefone { get; init; }

	public string? Email { get; init; }
}

/// <summary>
/// Empresa processadora: nome até 200, contato até 100.
/// </summary>
public sealed class EmpresaProcessadoraDados
{
	public string? Nome { get; init; }

	public string? Contato { get; init; }

	public string? Telefone { get; init; }

	public string? Email { get; init; }

	public EnderecoDados? Endereco { get; init; }
}

/// <summary>
/// Órgão na pesquisa.
/// </summary>
public sealed class OrgaoResumoDto
{
	public decimal CdOrg { get; init; }

	public string Nome { get; init; } = string.Empty;

	public string? Sigla { get; init; }

	public string? Uf { get; init; }

	public decimal? CdFil { get; init; }

	/// <summary>1 = ATIVO, 0 = INATIVO.</summary>
	public decimal? CdSituacao { get; init; }

	/// <summary>ATIVO ou INATIVO.</summary>
	public string? Situacao { get; init; }
}

/// <summary>
/// Filtros da pesquisa de órgãos.
/// </summary>
public sealed class ConsultaOrgaosRequest
{
	/// <summary>
	/// Começo do nome, começo da sigla ou o código do órgão.
	/// </summary>
	public string? Busca { get; init; }

	public decimal? CdFil { get; init; }

	public string? Uf { get; init; }

	/// <summary>
	/// Situação do órgão: 1 = ATIVO, 0 = INATIVO (opcional).
	/// </summary>
	public decimal? CdSituacao { get; init; }

	public int Pagina { get; init; } = 1;

	public int TamanhoPagina { get; init; } = 50;
}

public sealed class ConsultaOrgaosResultado
{
	public int Pagina { get; init; }

	public int TamanhoPagina { get; init; }

	public int TotalRegistros { get; init; }

	public int TotalPaginas { get; init; }

	public IReadOnlyList<OrgaoResumoDto> Orgaos { get; init; } = Array.Empty<OrgaoResumoDto>();
}

/// <summary>
/// Lotação (T000LOTA). Na alteração, o código vem da rota.
/// </summary>
public sealed class LotacaoDados
{
	/// <summary>
	/// Código (CD_LOT, até 6 dígitos). Na inclusão, omitir para usar MAX + 1.
	/// </summary>
	public decimal? CdLot { get; init; }

	/// <summary>
	/// Descrição (DE_LOT, até 50). Obrigatória.
	/// </summary>
	public string? Descricao { get; init; }

	/// <summary>
	/// Situação da lotação (CD_SIT_LOT): 1 = ATIVO, 2 = INATIVO.
	/// Omitida na inclusão: 1 (ATIVO).
	/// </summary>
	public decimal? CdSituacao { get; init; }

	/// <summary>
	/// ATIVO ou INATIVO. Só na resposta.
	/// </summary>
	public string? Situacao { get; init; }
}

/// <summary>
/// Grupo (T009GRUP). Na alteração, o código vem da rota.
/// </summary>
public sealed class GrupoDados
{
	/// <summary>
	/// Código (CD_GP, até 999). Na inclusão, omitir para usar MAX + 1.
	/// </summary>
	public decimal? CdGrupo { get; init; }

	/// <summary>Descrição (DE_GP, até 30). Obrigatória.</summary>
	public string? Descricao { get; init; }

	/// <summary>
	/// Tipo (TP_GP): F = DIA FIXO, U = DIA ÚTIL. Obrigatório.
	/// </summary>
	/// <example>F</example>
	public string? Tipo { get; init; }

	/// <summary>
	/// Descrição do tipo (DIA FIXO ou DIA ÚTIL). Só na resposta.
	/// </summary>
	public string? DescricaoTipo { get; init; }

	/// <summary>
	/// Ordem (ID_ORD): A = ASCENDENTE, D = DESCENDENTE. Obrigatória.
	/// </summary>
	/// <example>A</example>
	public string? Ordem { get; init; }

	/// <summary>
	/// Descrição da ordem (ASCENDENTE ou DESCENDENTE). Só na resposta.
	/// </summary>
	public string? DescricaoOrdem { get; init; }

	/// <summary>
	/// Número de dias (NR_DD): 1 a 31. Obrigatório.
	/// </summary>
	/// <example>5</example>
	public decimal? NumeroDias { get; init; }
}

/// <summary>
/// Subórgão (T009SBOR). O código é gerado pelo banco (IDENTITY):
/// na inclusão é ignorado; na alteração vem da rota.
/// </summary>
public sealed class SubOrgaoDados
{
	/// <summary>Código (SQ_SBOR), gerado pelo banco.</summary>
	public decimal? SqSubOrgao { get; init; }

	/// <summary>Nome (NM_SBOR, até 100). Obrigatório.</summary>
	public string? Nome { get; init; }
}

/// <summary>
/// Cargo (T000CARG). Na alteração, o código vem da rota.
/// </summary>
public sealed class CargoDados
{
	/// <summary>
	/// Código (CD_CG, até 4 dígitos). Na inclusão, omitir para usar MAX + 1.
	/// </summary>
	public decimal? CdCargo { get; init; }

	/// <summary>
	/// Descrição (DE_CG, até 30). Obrigatória.
	/// </summary>
	public string? Descricao { get; init; }

	/// <summary>
	/// Situação do cargo (CD_SIT_CAG): 1 = ATIVO, 0 = INATIVO.
	/// Omitida na inclusão: 1 (ATIVO).
	/// </summary>
	public decimal? CdSituacao { get; init; }

	/// <summary>
	/// ATIVO ou INATIVO. Só na resposta.
	/// </summary>
	public string? Situacao { get; init; }
}

/// <summary>
/// Vínculo do associado com um órgão (T000RPOR).
/// Na alteração, a sequência vem da rota.
/// </summary>
public sealed class VinculoOrgaoDados
{
	/// <summary>
	/// Órgão (CD_ORG), de GET /api/orgaos. Obrigatório.
	/// </summary>
	public decimal? CdOrg { get; init; }

	/// <summary>
	/// Lotação (CD_LOT), de GET /api/orgaos/lotacoes.
	/// </summary>
	public decimal? CdLotacao { get; init; }

	/// <summary>
	/// Situação do vínculo (CD_SIT_ORG), de GET /api/orgaos/situacoes.
	/// </summary>
	public decimal? CdSituacao { get; init; }

	/// <summary>Cargo (CD_CG), de GET /api/orgaos/cargos.</summary>
	public decimal? CdCargo { get; init; }

	/// <summary>Matrícula (MT_PES, até 20).</summary>
	public string? Matricula { get; init; }

	/// <summary>Dígito da matrícula (DV_MT, até 5).</summary>
	public string? DigitoMatricula { get; init; }

	/// <summary>Matrícula do instituidor (MT_INS, até 20).</summary>
	public string? MatriculaInstituidor { get; init; }

	/// <summary>Salário bruto (VR_SAL).</summary>
	public decimal? Salario { get; init; }

	/// <summary>Salário líquido (VR_SAL_LIQ).</summary>
	public decimal? SalarioLiquido { get; init; }
}

/// <summary>
/// Vínculo do associado com um órgão, com as descrições.
/// </summary>
public sealed class VinculoOrgaoDto
{
	/// <summary>
	/// Sequência do vínculo da pessoa (SQ_ORG).
	/// </summary>
	public decimal SqOrg { get; init; }

	public decimal CdOrg { get; init; }

	public string? NomeOrgao { get; init; }

	public string? SiglaOrgao { get; init; }

	public decimal? CdLotacao { get; init; }

	public string? Lotacao { get; init; }

	public decimal? CdSituacao { get; init; }

	public string? Situacao { get; init; }

	public decimal? CdCargo { get; init; }

	public string? Cargo { get; init; }

	public string? Matricula { get; init; }

	public string? DigitoMatricula { get; init; }

	public string? MatriculaInstituidor { get; init; }

	public decimal? Salario { get; init; }

	public decimal? SalarioLiquido { get; init; }
}
