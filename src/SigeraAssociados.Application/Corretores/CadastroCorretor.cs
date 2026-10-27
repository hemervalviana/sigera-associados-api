using SigeraAssociados.Application.Associados;

namespace SigeraAssociados.Application.Corretores;

/// <summary>
/// Dados de inclusão e alteração de um corretor. A API grava em T000PESS
/// (pessoa), em T000PFIS (pessoa física) OU T000PJUR (pessoa jurídica), e
/// em T009CRRT (corretor), numa única transação. O corretor é pessoa física
/// ou jurídica, nunca as duas: informe só o bloco do tipo escolhido.
/// Empresa, código (SQ_PES = MAX + 1), data e usuário do cadastro são
/// preenchidos pela API.
/// </summary>
public sealed class CadastroCorretorDados
{
	/// <summary>
	/// Tipo de pessoa (TP_PES): F = física (envie pessoaFisica) ou
	/// J = jurídica (envie pessoaJuridica). Obrigatório; não muda na alteração.
	/// </summary>
	/// <example>F</example>
	public string? TipoPessoa { get; init; }

	/// <summary>Nome ou razão social (T000PESS.NM_PES, até 80). Obrigatório.</summary>
	public string? Nome { get; init; }

	/// <summary>Nome curto ou nome fantasia (NC_PES, até 20).</summary>
	public string? NomeCurto { get; init; }

	/// <summary>E-mail (DE_COR_ELE, até 50).</summary>
	public string? Email { get; init; }

	/// <summary>Site (DE_URL, até 50).</summary>
	public string? Url { get; init; }

	/// <summary>Observação (OB_PES, até 255).</summary>
	public string? Observacao { get; init; }

	/// <summary>
	/// Pessoa física (T000PFIS), só quando tipoPessoa = F. No corretor, só o
	/// CPF é obrigatório; data de nascimento e sexo são opcionais.
	/// </summary>
	public PessoaFisicaDados? PessoaFisica { get; init; }

	/// <summary>Pessoa jurídica (T000PJUR), só quando tipoPessoa = J.</summary>
	public PessoaJuridicaDados? PessoaJuridica { get; init; }

	/// <summary>Dados do corretor (T009CRRT).</summary>
	public DadosCorretor? Corretor { get; init; }
}

/// <summary>
/// Dados de T000PJUR.
/// </summary>
public sealed class PessoaJuridicaDados
{
	/// <summary>CNPJ com ou sem pontuação (NR_CPJ). Obrigatório e único na base.</summary>
	/// <example>11.222.333/0001-81</example>
	public string? Cnpj { get; init; }

	/// <summary>Inscrição estadual (NR_CGF, até 9 dígitos).</summary>
	public decimal? InscricaoEstadual { get; init; }

	/// <summary>Nome do contato (NM_CON, até 50).</summary>
	public string? Contato { get; init; }

	/// <summary>
	/// Cargo do contato (CD_CG), de GET /api/corretores/cargos (T000CARG).
	/// </summary>
	public decimal? CdCargo { get; init; }

	/// <summary>Descrição do cargo. Só na resposta.</summary>
	public string? Cargo { get; init; }
}

/// <summary>
/// Dados de T009CRRT. Os nomes das comissões seguem as colunas do legado.
/// </summary>
public sealed class DadosCorretor
{
	/// <summary>Filial (CD_FIL), de GET /api/cadastros/filiais.</summary>
	public decimal? CdFil { get; init; }

	/// <summary>Município de atuação (CD_MUN), de GET /api/cadastros/municipios.</summary>
	public decimal? CdMunicipio { get; init; }

	/// <summary>
	/// Situação (CD_SIT_CRRT): 1 = ATIVO, 2 = INATIVO. Omitida na inclusão: 1.
	/// </summary>
	public decimal? CdSituacao { get; init; }

	/// <summary>ATIVO ou INATIVO. Só na resposta.</summary>
	public string? Situacao { get; init; }

	/// <summary>Classe (CL_CRO, 1 caractere, valores do legado).</summary>
	public string? Classe { get; init; }

	/// <summary>Tipo (TP_CRRT): 1 = CORRETOR, 2 = COLABORADOR.</summary>
	public int? Tipo { get; init; }

	/// <summary>CORRETOR ou COLABORADOR. Só na resposta.</summary>
	public string? DescricaoTipo { get; init; }

	/// <summary>Inscrição na SUSEP (NR_ISC_SUS, até 15 dígitos).</summary>
	public decimal? InscricaoSusep { get; init; }

	/// <summary>Percentual de comissão PC_CMS_ADT (0 a 100, 2 casas).</summary>
	public decimal? ComissaoAdesao { get; init; }

	/// <summary>Percentual de comissão PC_CMS_CAR (0 a 100, 2 casas).</summary>
	public decimal? ComissaoCarencia { get; init; }

	/// <summary>Percentual de comissão PC_CMS_PLN (0 a 100, 2 casas).</summary>
	public decimal? ComissaoPlano { get; init; }

	/// <summary>
	/// Corretor representante (SQ_PES_RPR): outro corretor, pessoa jurídica,
	/// não excluído e da mesma filial (exige cdFil). Nunca o próprio corretor.
	/// </summary>
	public decimal? SqPesRepresentante { get; init; }

	/// <summary>
	/// Senha do representante (SH_RPR, até 20). Gravada em texto puro, como no
	/// legado, sem aparar. Na alteração: omitida (null) mantém a atual; "" apaga.
	/// Volta só no detalhe, para Administradores e Gestores; nunca na
	/// pesquisa nem nos logs.
	/// </summary>
	public string? SenhaRepresentante { get; init; }

	/// <summary>Coordenador (SQ_CDR), conferido em T009COOR.</summary>
	public decimal? SqCoordenador { get; init; }

	/// <summary>Agente (SQ_AGEN), de GET /api/corretores/agentes.</summary>
	public decimal? SqAgente { get; init; }

	/// <summary>Nome do agente. Só na resposta.</summary>
	public string? Agente { get; init; }

	/// <summary>Gerente de vendas (SQ_GVED), de GET /api/corretores/gerentes-vendas.</summary>
	public decimal? SqGerenteVendas { get; init; }

	/// <summary>Nome do gerente de vendas. Só na resposta.</summary>
	public string? GerenteVendas { get; init; }
}

/// <summary>
/// Cadastro completo do corretor, como a API devolve.
/// </summary>
public sealed class CorretorCadastroDto
{
	public decimal SqPesCorretor { get; init; }

	/// <summary>F (física) ou J (jurídica).</summary>
	public string TipoPessoa { get; init; } = string.Empty;

	public string Nome { get; init; } = string.Empty;

	public string? NomeCurto { get; init; }

	public string? Email { get; init; }

	public string? Url { get; init; }

	public string? Observacao { get; init; }

	public DateTime DataCadastro { get; init; }

	public string? UsuarioCadastro { get; init; }

	/// <summary>Só no corretor pessoa física; CPF completo e formatado.</summary>
	public PessoaFisicaDados? PessoaFisica { get; init; }

	/// <summary>Só no corretor pessoa jurídica; CNPJ formatado.</summary>
	public PessoaJuridicaDados? PessoaJuridica { get; init; }

	public DadosCorretor Corretor { get; init; } = new();

	/// <summary>Nome do corretor representante (em maiúsculas, como no legado), quando há.</summary>
	public string? NomeRepresentante { get; init; }
}

/// <summary>
/// Corretor na pesquisa (sem senha).
/// </summary>
public sealed class CorretorResumoDto
{
	public decimal SqPesCorretor { get; init; }

	public string Nome { get; init; } = string.Empty;

	public string? NomeCurto { get; init; }

	public string TipoPessoa { get; init; } = string.Empty;

	/// <summary>CPF ou CNPJ formatado.</summary>
	public string? Documento { get; init; }

	public decimal? CdFil { get; init; }

	public decimal? CdSituacao { get; init; }

	/// <summary>ATIVO ou INATIVO.</summary>
	public string? Situacao { get; init; }

	public string? Classe { get; init; }

	public int? Tipo { get; init; }

	/// <summary>CORRETOR ou COLABORADOR.</summary>
	public string? DescricaoTipo { get; init; }

	public decimal? SqAgente { get; init; }

	/// <summary>Nome do agente.</summary>
	public string? Agente { get; init; }

	public decimal? SqGerenteVendas { get; init; }

	/// <summary>Nome do gerente de vendas.</summary>
	public string? GerenteVendas { get; init; }
}

/// <summary>
/// Parametrização do corretor: só classificação, status, agente e gerente
/// (T009CRRT.TP_CRRT, CD_SIT_CRRT, SQ_AGEN e SQ_GVED). Envie sempre os
/// quatro: agente ou gerente nulo limpa o campo.
/// </summary>
public sealed class ParametrizacaoCorretorDados
{
	/// <summary>Classificação (TP_CRRT): 1 = CORRETOR, 2 = COLABORADOR. Obrigatória.</summary>
	public int? Tipo { get; init; }

	/// <summary>Status (CD_SIT_CRRT): 1 = ATIVO, 2 = INATIVO. Obrigatório.</summary>
	public decimal? CdSituacao { get; init; }

	/// <summary>Agente (SQ_AGEN), de GET /api/corretores/agentes. Nulo: sem agente.</summary>
	public decimal? SqAgente { get; init; }

	/// <summary>Gerente de vendas (SQ_GVED), de GET /api/corretores/gerentes-vendas. Nulo: sem gerente.</summary>
	public decimal? SqGerenteVendas { get; init; }
}

public sealed class PesquisaCorretoresRequest
{
	/// <summary>Começo do nome, código do corretor, CPF ou CNPJ.</summary>
	public string? Busca { get; init; }

	public decimal? CdFil { get; init; }

	/// <summary>F ou J (opcional).</summary>
	public string? TipoPessoa { get; init; }

	public decimal? CdSituacao { get; init; }

	/// <summary>Classificação (TP_CRRT): 0 ou omitido = TODOS, 1 = CORRETOR, 2 = COLABORADOR.</summary>
	public int? Tipo { get; init; }

	/// <summary>Agente (SQ_AGEN) (opcional).</summary>
	public decimal? SqAgente { get; init; }

	/// <summary>Gerente de vendas (SQ_GVED) (opcional).</summary>
	public decimal? SqGerenteVendas { get; init; }

	public int Pagina { get; init; } = 1;

	public int TamanhoPagina { get; init; } = 50;
}

public sealed class PesquisaCorretoresResultado
{
	public int Pagina { get; init; }

	public int TamanhoPagina { get; init; }

	public int TotalRegistros { get; init; }

	public int TotalPaginas { get; init; }

	public IReadOnlyList<CorretorResumoDto> Corretores { get; init; } = Array.Empty<CorretorResumoDto>();
}
