namespace SigeraAssociados.Application.Associados;

/// <summary>
/// Dados de inclusão e alteração de um associado pessoa física.
/// A API grava em T000PESS (pessoa), T000PFIS (pessoa física) e
/// T000CLIE (associado), numa única transação.
/// Empresa (CD_EMP), tipo de pessoa (F), código (SQ_PES = MAX + 1),
/// data e usuário do cadastro são preenchidos pela API.
/// </summary>
public sealed class CadastroAssociadoDados
{
	/// <summary>
	/// Nome (T000PESS.NM_PES, até 80). Obrigatório.
	/// </summary>
	/// <example>MARIA DA SILVA</example>
	public string? Nome { get; init; }

	/// <summary>
	/// Nome curto (T000PESS.NC_PES, até 20).
	/// </summary>
	public string? NomeCurto { get; init; }

	/// <summary>
	/// E-mail (T000PESS.DE_COR_ELE, até 50).
	/// </summary>
	public string? Email { get; init; }

	/// <summary>
	/// Observação (T000PESS.OB_PES, até 255).
	/// </summary>
	public string? Observacao { get; init; }

	/// <summary>
	/// Só na inclusão: corretor a quem o associado fica vinculado
	/// (T000CLIE.SQ_PES_CRO_CAD). Para quem tem acesso pela carteira de
	/// corretor, é preenchido sozinho; informe quando houver mais de um
	/// corretor liberado na filial. Ignorado na alteração.
	/// </summary>
	public decimal? SqPesCorretor { get; init; }

	public PessoaFisicaDados? PessoaFisica { get; init; }

	public AssociadoDados? Associado { get; init; }
}

/// <summary>
/// Dados de T000PFIS.
/// </summary>
public sealed class PessoaFisicaDados
{
	/// <summary>
	/// CPF com ou sem pontuação (NR_CPF). Obrigatório e único na base.
	/// </summary>
	/// <example>123.456.789-09</example>
	public string? Cpf { get; init; }

	/// <summary>
	/// RG (NR_RG, até 40).
	/// </summary>
	public string? Rg { get; init; }

	/// <summary>
	/// Órgão expedidor (NM_ORG_EXP, até 10).
	/// </summary>
	public string? OrgaoExpedidor { get; init; }

	/// <summary>
	/// UF do órgão expedidor (SG_UF_ORG, até 8).
	/// </summary>
	public string? UfOrgao { get; init; }

	/// <summary>
	/// Data de nascimento (DT_NAS). Obrigatória.
	/// </summary>
	public DateTime? DataNascimento { get; init; }

	/// <summary>
	/// Sexo (ID_SEX): M ou F. Obrigatório.
	/// </summary>
	public string? Sexo { get; init; }

	/// <summary>
	/// Estado civil (TP_EST_CIV, 1 letra).
	/// </summary>
	public string? EstadoCivil { get; init; }

	/// <summary>
	/// Município (CD_MUN), de GET /api/cadastros/municipios.
	/// </summary>
	public decimal? CdMunicipio { get; init; }

	/// <summary>
	/// Tratamento (CD_TRT), de GET /api/cadastros/tratamentos.
	/// </summary>
	public decimal? CdTratamento { get; init; }

	/// <summary>
	/// Nome do pai (NM_PAI, até 50).
	/// </summary>
	public string? NomePai { get; init; }

	/// <summary>
	/// Nome da mãe (NM_MAE, até 50).
	/// </summary>
	public string? NomeMae { get; init; }

	/// <summary>
	/// Nome do cônjuge (NM_CJG, até 50).
	/// </summary>
	public string? NomeConjuge { get; init; }

	/// <summary>
	/// Data de nascimento do cônjuge (DT_NAS_CJG).
	/// </summary>
	public DateTime? DataNascimentoConjuge { get; init; }

	/// <summary>
	/// Data do casamento (DT_CAS).
	/// </summary>
	public DateTime? DataCasamento { get; init; }
}

/// <summary>
/// Dados de T000CLIE.
/// </summary>
public sealed class AssociadoDados
{
	/// <summary>
	/// Filial (CD_FIL), de GET /api/cadastros/filiais. Obrigatória.
	/// </summary>
	public decimal? CdFil { get; init; }

	/// <summary>
	/// Situação (CD_SIT_CLI), de GET /api/cadastros/situacoes. Obrigatória.
	/// 10 (falecido) só por Administradores e Gestores.
	/// </summary>
	public decimal? CdSitCli { get; init; }

	/// <summary>
	/// Observação da situação (OB_SIT, até 100).
	/// </summary>
	public string? ObsSituacao { get; init; }

	/// <summary>
	/// Data da carteira (DT_CARTEIRA).
	/// </summary>
	public DateTime? DataCarteira { get; init; }

	/// <summary>
	/// Senha do portal (NR_PWS, até 50). Gravada em texto puro, como no
	/// legado, que lê o valor como está. Não é aparada.
	/// Na alteração: omitida (null) mantém a atual; "" apaga.
	/// Volta só no detalhe do associado (GET /api/associados/{sqPesCli}) e nas
	/// respostas de inclusão e alteração; nunca na pesquisa nem nos logs.
	/// </summary>
	public string? SenhaPortal { get; init; }

	/// <summary>
	/// Senha de averbação (NR_PWS_02, até 50). Mesmas regras da senha do portal.
	/// </summary>
	public string? SenhaAverbacao { get; init; }
}
