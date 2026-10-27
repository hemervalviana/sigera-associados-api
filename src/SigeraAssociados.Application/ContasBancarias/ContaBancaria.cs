namespace SigeraAssociados.Application.ContasBancarias;

/// <summary>
/// Dados de inclusão e alteração de uma conta bancária do associado (T000CCOR).
/// Na alteração, a sequência vem da rota.
/// </summary>
public sealed class ContaBancariaDados
{
	/// <summary>Banco (CD_BCO), de GET /api/cadastros/bancos. Obrigatório.</summary>
	/// <example>104</example>
	public decimal? CdBanco { get; init; }

	/// <summary>Dígito do banco (DV_BCO, 1 caractere).</summary>
	public string? DigitoBanco { get; init; }

	/// <summary>Agência (CD_AG, até 5 dígitos). Obrigatória.</summary>
	/// <example>1234</example>
	public decimal? Agencia { get; init; }

	/// <summary>Dígito da agência (DV_AG, 1 caractere).</summary>
	public string? DigitoAgencia { get; init; }

	/// <summary>Número da conta (NR_CTA_COR, até 13 dígitos). Obrigatório.</summary>
	/// <example>123456</example>
	public decimal? NumeroConta { get; init; }

	/// <summary>Dígito da conta (DV_CTA_COR, até 2 caracteres).</summary>
	public string? DigitoConta { get; init; }

	/// <summary>Operação (OP_BCO, até 5; ex.: 001 ou 013 na Caixa).</summary>
	public string? Operacao { get; init; }

	/// <summary>
	/// Tipo de conta (TP_CTA_COR): 0 = SEM CONTA, 1 = CONTA CORRENTE, 2 = POUPANÇA.
	/// </summary>
	/// <example>1</example>
	public decimal? CdTipoConta { get; init; }

	/// <summary>Titular (DE_TIT_COR, até 100).</summary>
	public string? Titular { get; init; }

	/// <summary>Chave PIX (DE_PIX, até 100).</summary>
	public string? ChavePix { get; init; }

	/// <summary>Identificador (NR_IDT, até 25).</summary>
	public string? Identificador { get; init; }

	/// <summary>Identificador CEF (NR_IDT_02, até 25).</summary>
	public string? IdentificadorCef { get; init; }

	/// <summary>
	/// Situação da conta (ID_CC_ATV): 1 = ATIVA, 2 = INATIVA.
	/// Omitida na inclusão: 1 (ATIVA).
	/// </summary>
	/// <example>1</example>
	public decimal? CdSituacao { get; init; }

	/// <summary>
	/// Enviar optante CEF? (env_opt_cax: true grava 2 = SIM, false grava 1 = NÃO).
	/// </summary>
	public bool? EnviarOptanteCef { get; init; }
}

/// <summary>
/// Conta bancária do associado, com o nome do banco.
/// </summary>
public sealed class ContaBancariaDto
{
	/// <summary>Sequência da conta da pessoa (SQ_CTA_COR).</summary>
	public decimal SqConta { get; init; }

	public decimal CdBanco { get; init; }

	public string? Banco { get; init; }

	public string? DigitoBanco { get; init; }

	public decimal Agencia { get; init; }

	public string? DigitoAgencia { get; init; }

	public decimal NumeroConta { get; init; }

	public string? DigitoConta { get; init; }

	public string? Operacao { get; init; }

	/// <summary>0 = SEM CONTA, 1 = CONTA CORRENTE, 2 = POUPANÇA.</summary>
	public decimal? CdTipoConta { get; init; }

	/// <summary>SEM CONTA, CONTA CORRENTE ou POUPANÇA.</summary>
	public string? TipoConta { get; init; }

	public string? Titular { get; init; }

	public string? ChavePix { get; init; }

	/// <summary>NR_IDT.</summary>
	public string? Identificador { get; init; }

	/// <summary>NR_IDT_02.</summary>
	public string? IdentificadorCef { get; init; }

	/// <summary>1 = ATIVA, 2 = INATIVA.</summary>
	public decimal? CdSituacao { get; init; }

	/// <summary>ATIVA ou INATIVA.</summary>
	public string? Situacao { get; init; }

	/// <summary>env_opt_cax: 2 = SIM (true), 1 = NÃO (false); null quando vazio ou desconhecido.</summary>
	public bool? EnviarOptanteCef { get; init; }
}

/// <summary>
/// Valores fixos da conta bancária (T000CCOR), definidos pelo sistema
/// legado, sem tabela no banco.
/// </summary>
public static class RegrasContaBancaria
{
	public const decimal TipoSemConta = 0;
	public const decimal TipoContaCorrente = 1;
	public const decimal TipoPoupanca = 2;

	public const decimal Ativa = 1;
	public const decimal Inativa = 2;

	/// <summary>env_opt_cax = SIM.</summary>
	public const decimal OptanteCefSim = 2;

	/// <summary>env_opt_cax = NÃO.</summary>
	public const decimal OptanteCefNao = 1;

	public static string? DescricaoTipo(decimal? codigo) => codigo switch
	{
		TipoSemConta => "SEM CONTA",
		TipoContaCorrente => "CONTA CORRENTE",
		TipoPoupanca => "POUPANÇA",
		_ => null
	};

	public static string? DescricaoSituacao(decimal? codigo) => codigo switch
	{
		Ativa => "ATIVA",
		Inativa => "INATIVA",
		_ => null
	};

	public static decimal? CodigoOptanteCef(bool? enviar) => enviar switch
	{
		true => OptanteCefSim,
		false => OptanteCefNao,
		null => null
	};

	public static bool? EnviarOptanteCef(decimal? codigo) => codigo switch
	{
		OptanteCefSim => true,
		OptanteCefNao => false,
		_ => null
	};
}

/// <summary>
/// Regras de formato da conta bancária (sem acesso ao banco de dados).
/// </summary>
public static class ValidacaoContaBancaria
{
	/// <summary>
	/// Quantidade máxima de contas por pessoa (SQ_CTA_COR é numeric(2,0)).
	/// </summary>
	public const int MaximoContas = 99;

	public static string? Validar(ContaBancariaDados? dados)
	{
		if (dados is null)
		{
			return "Informe os dados da conta.";
		}

		if (dados.CdBanco is null)
		{
			return "O banco (cdBanco) é obrigatório.";
		}

		if (dados.Agencia is null)
		{
			return "A agência é obrigatória.";
		}

		if (dados.NumeroConta is null)
		{
			return "O número da conta (numeroConta) é obrigatório.";
		}

		return Inteiro(dados.CdBanco, 1, 999, "cdBanco")
			?? Inteiro(dados.Agencia, 0, 99_999, "agencia")
			?? Inteiro(dados.NumeroConta, 1, 9_999_999_999_999m, "numeroConta")
			?? (dados.CdTipoConta is null || RegrasContaBancaria.DescricaoTipo(dados.CdTipoConta) is not null
				? null
				: "O campo cdTipoConta deve ser 0 (SEM CONTA), 1 (CONTA CORRENTE) ou 2 (POUPANÇA).")
			?? (dados.CdSituacao is null || RegrasContaBancaria.DescricaoSituacao(dados.CdSituacao) is not null
				? null
				: "O campo cdSituacao deve ser 1 (ATIVA) ou 2 (INATIVA).")
			?? Tamanho(dados.DigitoBanco, 1, "digitoBanco")
			?? Tamanho(dados.DigitoAgencia, 1, "digitoAgencia")
			?? Tamanho(dados.DigitoConta, 2, "digitoConta")
			?? Tamanho(dados.Operacao, 5, "operacao")
			?? Tamanho(dados.Titular, 100, "titular")
			?? Tamanho(dados.ChavePix, 100, "chavePix")
			?? Tamanho(dados.Identificador, 25, "identificador")
			?? Tamanho(dados.IdentificadorCef, 25, "identificadorCef");
	}

	private static string? Inteiro(decimal? valor, decimal minimo, decimal maximo, string campo) =>
		valor is not null && (valor < minimo || valor > maximo || valor % 1 != 0)
			? $"O campo {campo} deve ser um número inteiro de {minimo:0} a {maximo:0}."
			: null;

	private static string? Tamanho(string? valor, int maximo, string campo) =>
		valor is not null && valor.Trim().Length > maximo
			? $"O campo {campo} aceita no máximo {maximo} caractere(s)."
			: null;
}
