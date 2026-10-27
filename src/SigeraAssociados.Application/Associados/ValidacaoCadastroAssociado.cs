namespace SigeraAssociados.Application.Associados;

/// <summary>
/// Regras de formato do cadastro de associado (sem acesso ao banco).
/// A existência de filial, situação, município, tratamento e a unicidade
/// do CPF são conferidas na gravação.
/// </summary>
public static class ValidacaoCadastroAssociado
{
	/// <summary>
	/// Situação "FALECIDO" (T000SCLI). Ao gravar, a trigger TR_CLIE_UPD
	/// encerra os contratos ativos do associado.
	/// </summary>
	public const decimal SituacaoFalecido = 10;

	// Faixa do tipo smalldatetime do SQL Server.
	private static readonly DateTime DataMinima = new(1900, 1, 1);
	private static readonly DateTime DataMaxima = new(2079, 6, 6);

	/// <summary>
	/// Devolve a primeira mensagem de erro, ou null quando os dados são válidos.
	/// </summary>
	public static string? Validar(CadastroAssociadoDados? dados)
	{
		if (dados is null)
		{
			return "Informe os dados do associado.";
		}

		var erro =
			Obrigatorio(dados.Nome, "nome")
			?? Tamanho(dados.Nome, 80, "nome")
			?? Tamanho(dados.NomeCurto, 20, "nomeCurto")
			?? Tamanho(dados.Email, 50, "email")
			?? Tamanho(dados.Observacao, 255, "observacao")
			?? Codigo(dados.SqPesCorretor, 9_999_999, "sqPesCorretor");

		if (erro is not null)
		{
			return erro;
		}

		if (!string.IsNullOrWhiteSpace(dados.Email) && !dados.Email.Contains('@'))
		{
			return "O e-mail é inválido.";
		}

		if (dados.PessoaFisica is not { } pf)
		{
			return "Informe os dados de pessoa física (pessoaFisica).";
		}

		if (dados.Associado is not { } associado)
		{
			return "Informe os dados do associado (associado).";
		}

		if (string.IsNullOrWhiteSpace(pf.Cpf))
		{
			return "O CPF é obrigatório.";
		}

		if (!DocumentoCpf.Valido(DocumentoCpf.SomenteDigitos(pf.Cpf)))
		{
			return "CPF inválido. Informe os 11 números do CPF, com ou sem pontuação.";
		}

		if (pf.DataNascimento is not DateTime nascimento)
		{
			return "A data de nascimento é obrigatória.";
		}

		if (nascimento.Date > DateTime.Today || nascimento < DataMinima)
		{
			return "A data de nascimento é inválida.";
		}

		if (string.IsNullOrWhiteSpace(pf.Sexo)
			|| pf.Sexo.Trim().ToUpperInvariant() is not ("M" or "F"))
		{
			return "O sexo é obrigatório: M ou F.";
		}

		erro =
			Tamanho(pf.EstadoCivil, 1, "estadoCivil")
			?? Tamanho(pf.Rg, 40, "rg")
			?? Tamanho(pf.OrgaoExpedidor, 10, "orgaoExpedidor")
			?? Tamanho(pf.UfOrgao, 8, "ufOrgao")
			?? Tamanho(pf.NomePai, 50, "nomePai")
			?? Tamanho(pf.NomeMae, 50, "nomeMae")
			?? Tamanho(pf.NomeConjuge, 50, "nomeConjuge")
			?? Codigo(pf.CdMunicipio, 9_999_999, "cdMunicipio")
			?? Codigo(pf.CdTratamento, 99, "cdTratamento")
			?? DataOpcional(pf.DataNascimentoConjuge, "dataNascimentoConjuge")
			?? DataOpcional(pf.DataCasamento, "dataCasamento");

		if (erro is not null)
		{
			return erro;
		}

		if (associado.CdFil is null)
		{
			return "A filial é obrigatória.";
		}

		if (associado.CdSitCli is null)
		{
			return "A situação é obrigatória.";
		}

		return Codigo(associado.CdFil, 99, "cdFil")
			?? Codigo(associado.CdSitCli, 99, "cdSitCli")
			?? Tamanho(associado.ObsSituacao, 100, "obsSituacao")
			?? Senha(associado.SenhaPortal, "senhaPortal")
			?? Senha(associado.SenhaAverbacao, "senhaAverbacao");
	}

	/// <summary>
	/// Tamanho máximo das senhas do portal (NR_PWS) e de averbação (NR_PWS_02), varchar(50).
	/// </summary>
	public const int TamanhoSenha = 50;

	/// <summary>
	/// Senha opcional: até 50 caracteres, sem caracteres de controle e só
	/// com caracteres que o varchar do legado guarda sem trocar (Latin-1).
	/// O valor não é aparado: espaços contam.
	/// </summary>
	private static string? Senha(string? valor, string campo)
	{
		if (valor is null || valor.Length == 0)
		{
			return null;
		}

		if (valor.Length > TamanhoSenha)
		{
			return $"O campo {campo} aceita no máximo {TamanhoSenha} caracteres.";
		}

		return valor.All(ch => ch is >= ' ' and <= '~' or >= '\u00A0' and <= '\u00FF')
			? null
			: $"O campo {campo} tem caracteres não aceitos (use letras, números e símbolos comuns).";
	}

	private static string? Obrigatorio(string? valor, string campo) =>
		string.IsNullOrWhiteSpace(valor) ? $"O campo {campo} é obrigatório." : null;

	private static string? Tamanho(string? valor, int maximo, string campo) =>
		valor is not null && valor.Trim().Length > maximo
			? $"O campo {campo} aceita no máximo {maximo} caracteres."
			: null;

	private static string? Codigo(decimal? valor, decimal maximo, string campo) =>
		valor is not null && (valor < 1 || valor > maximo || valor % 1 != 0)
			? $"O campo {campo} deve ser um código inteiro entre 1 e {maximo:0}."
			: null;

	private static string? DataOpcional(DateTime? valor, string campo) =>
		valor is DateTime data && (data.Date > DateTime.Today || data < DataMinima || data > DataMaxima)
			? $"O campo {campo} é inválido."
			: null;
}
