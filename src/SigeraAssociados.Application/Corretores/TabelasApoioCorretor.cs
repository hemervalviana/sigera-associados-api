using SigeraAssociados.Application.Financeiro;

namespace SigeraAssociados.Application.Corretores;

/// <summary>
/// Tipo do corretor (T009CRRT.TP_CRRT): valores fixos do legado, sem tabela.
/// </summary>
public static class TipoCorretor
{
	public const int Corretor = 1;
	public const int Colaborador = 2;

	public static string? Descricao(int? tipo) => tipo switch
	{
		Corretor => "CORRETOR",
		Colaborador => "COLABORADOR",
		_ => null
	};

	public static bool Valido(int? tipo) => tipo is null or Corretor or Colaborador;
}

/// <summary>
/// Agente (T009AGENT), com os dados bancários dele. Na alteração, o código vem da rota.
/// </summary>
public sealed class AgenteDados
{
	/// <summary>Código (SQ_AGEN, até 8 dígitos). Na inclusão, omitir para usar MAX + 1.</summary>
	public decimal? SqAgente { get; init; }

	/// <summary>Nome do agente (DE_AGEN, até 100). Obrigatório.</summary>
	public string? Descricao { get; init; }

	/// <summary>Banco (CD_BCO), de GET /api/cadastros/bancos.</summary>
	public decimal? CdBanco { get; init; }

	/// <summary>Dígito do banco (DV_BCO, 1 caractere).</summary>
	public string? DigitoBanco { get; init; }

	/// <summary>Agência (CD_AG, até 5 dígitos).</summary>
	public decimal? Agencia { get; init; }

	/// <summary>Dígito da agência (DV_AG, 1 caractere).</summary>
	public string? DigitoAgencia { get; init; }

	/// <summary>Conta corrente (NR_CTA_COR, até 13 dígitos).</summary>
	public decimal? Conta { get; init; }

	/// <summary>Dígito da conta (DV_CTA_COR, até 2 caracteres).</summary>
	public string? DigitoConta { get; init; }

	/// <summary>Operação (OP_BCO, até 3 dígitos; ex.: 013 da Caixa).</summary>
	public decimal? Operacao { get; init; }
}

/// <summary>
/// Gerente de vendas (T009GVED). Na alteração, o código vem da rota.
/// </summary>
public sealed class GerenteVendasDados
{
	/// <summary>Código (SQ_GVED, até 8 dígitos). Na inclusão, omitir para usar MAX + 1.</summary>
	public decimal? SqGerenteVendas { get; init; }

	/// <summary>Nome do gerente (DE_GVED, até 100). Obrigatório.</summary>
	public string? Descricao { get; init; }

	/// <summary>Percentual de comissão (VR_PC_CMS, 0 a 100, 2 casas).</summary>
	public decimal? PercentualComissao { get; init; }
}

/// <summary>
/// Regras de formato das tabelas de apoio do corretor (sem acesso ao banco).
/// </summary>
public static class ValidacaoApoioCorretor
{
	public const decimal CodigoMaximo = 99_999_999;

	public static string? Agente(AgenteDados? d, bool inclusao)
	{
		if (d is null)
		{
			return "Informe os dados do agente.";
		}

		return (inclusao ? RegrasCampos.Inteiro(d.SqAgente, 1, CodigoMaximo, "sqAgente") : null)
			?? RegrasCampos.Obrigatorio(d.Descricao, "descricao")
			?? RegrasCampos.Tamanho(d.Descricao, 100, "descricao")
			?? RegrasCampos.Inteiro(d.CdBanco, 1, 999, "cdBanco")
			?? RegrasCampos.Tamanho(d.DigitoBanco, 1, "digitoBanco")
			?? RegrasCampos.Inteiro(d.Agencia, 0, 99_999, "agencia")
			?? RegrasCampos.Tamanho(d.DigitoAgencia, 1, "digitoAgencia")
			?? RegrasCampos.Inteiro(d.Conta, 0, 9_999_999_999_999m, "conta")
			?? RegrasCampos.Tamanho(d.DigitoConta, 2, "digitoConta")
			?? RegrasCampos.Inteiro(d.Operacao, 0, 999, "operacao");
	}

	public static string? GerenteVendas(GerenteVendasDados? d, bool inclusao)
	{
		if (d is null)
		{
			return "Informe os dados do gerente de vendas.";
		}

		return (inclusao ? RegrasCampos.Inteiro(d.SqGerenteVendas, 1, CodigoMaximo, "sqGerenteVendas") : null)
			?? RegrasCampos.Obrigatorio(d.Descricao, "descricao")
			?? RegrasCampos.Tamanho(d.Descricao, 100, "descricao")
			?? (d.PercentualComissao is decimal p && (p < 0 || p > 100 || decimal.Round(p, 2) != p)
				? "O campo percentualComissao deve ser um percentual de 0 a 100, com até 2 casas."
				: null);
	}
}
