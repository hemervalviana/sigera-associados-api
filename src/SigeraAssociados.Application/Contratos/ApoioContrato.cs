using SigeraAssociados.Application.Financeiro;

namespace SigeraAssociados.Application.Contratos;

/// <summary>Situação do contrato (T009SCTR).</summary>
public sealed class SituacaoContratoApoioDados
{
	/// <summary>Código (CD_ST_CTR, 1 a 99). Na inclusão, omitir para usar MAX + 1; na alteração vem da rota.</summary>
	public decimal? CdSituacao { get; init; }

	/// <summary>Descrição (DE_ST_CTR, até 50). Obrigatória.</summary>
	public string? Descricao { get; init; }
}

/// <summary>Carência do contrato (T009CARE).</summary>
public sealed class CarenciaDados
{
	/// <summary>Código (CD_CAR, 1 a 9). Na inclusão, omitir para usar MAX + 1; na alteração vem da rota.</summary>
	public decimal? CdCarencia { get; init; }

	/// <summary>Descrição (DE_CAR, até 30). Obrigatória.</summary>
	public string? Descricao { get; init; }
}

/// <summary>Origem do recurso (T009OREC).</summary>
public sealed class OrigemRecursoDados
{
	/// <summary>Código (CD_ORI_REC, 1 a 99999). Na inclusão, omitir para usar MAX + 1; na alteração vem da rota.</summary>
	public decimal? CdOrigemRecurso { get; init; }

	/// <summary>Descrição (DE_ORI_REC, até 50). Obrigatória.</summary>
	public string? Descricao { get; init; }

	/// <summary>Tipo (TP_ORI_REC, 1 caractere). Obrigatório.</summary>
	public string? Tipo { get; init; }
}

/// <summary>Produtor do corretor (T009PROD).</summary>
public sealed class ProdutorDados
{
	/// <summary>Código (SQ_PRD, 1 a 9999999). Na inclusão, omitir para usar MAX + 1; na alteração vem da rota.</summary>
	public decimal? SqProdutor { get; init; }

	/// <summary>Nome (NM_PRD, até 80). Obrigatório.</summary>
	public string? Descricao { get; init; }

	/// <summary>Corretor do produtor (SQ_PES_CRO), de GET /api/corretores. Obrigatório.</summary>
	public decimal? SqPesCorretor { get; init; }

	/// <summary>Nome do corretor (só na resposta).</summary>
	public string? Corretor { get; init; }
}

/// <summary>
/// Índice da mensalidade (T009INDI): mensalidade = valor do contrato x índice,
/// por carência e quantidade de parcelas.
/// </summary>
public sealed class IndiceDados
{
	/// <summary>Carência (CD_CAR), de GET /api/contratos/carencias. Na alteração vem da rota.</summary>
	public decimal? CdCarencia { get; init; }

	/// <summary>Quantidade de parcelas (QT_PCL, 1 a 9999). Na alteração vem da rota.</summary>
	public decimal? QuantidadeParcelas { get; init; }

	/// <summary>Índice (VR_IND), maior que zero, até 9,9999 com 4 casas. Obrigatório.</summary>
	public decimal? Indice { get; init; }
}

/// <summary>Regras de formato das tabelas de apoio do contrato (sem acesso ao banco).</summary>
public static class ValidacaoApoioContrato
{
	public const decimal CarenciaMaxima = 9;
	public const decimal OrigemMaxima = 99_999;
	public const decimal ProdutorMaximo = 9_999_999;
	public const decimal ParcelasMaximo = 9_999;

	public const decimal SituacaoMaxima = 99;

	/// <summary>
	/// Situações que a API usa por código (aprovado, renovação, encerrado,
	/// quitado, cancelado, finalizado, quitado por dentro): podem ter a
	/// descrição alterada, mas nunca ser excluídas.
	/// </summary>
	public static readonly IReadOnlySet<decimal> SituacoesFixas = new HashSet<decimal>
	{
		RegrasContrato.SituacaoAprovado, RegrasContrato.SituacaoRenovacao, RegrasContrato.SituacaoEncerrado,
		RegrasContrato.SituacaoQuitado, RegrasContrato.SituacaoCancelado, RegrasContrato.SituacaoFinalizado,
		RegrasContrato.SituacaoQuitadoPorDentro
	};

	public static string? Situacao(SituacaoContratoApoioDados? d, bool inclusao) =>
		d is null
			? "Informe a situação."
			: (inclusao ? RegrasCampos.Inteiro(d.CdSituacao, 1, SituacaoMaxima, "cdSituacao") : null)
				?? RegrasCampos.Obrigatorio(d.Descricao, "descricao")
				?? RegrasCampos.Tamanho(d.Descricao?.Trim(), 50, "descricao");

	public static string? Carencia(CarenciaDados? d, bool inclusao) =>
		d is null
			? "Informe a carência."
			: (inclusao ? RegrasCampos.Inteiro(d.CdCarencia, 1, CarenciaMaxima, "cdCarencia") : null)
				?? RegrasCampos.Obrigatorio(d.Descricao, "descricao")
				?? RegrasCampos.Tamanho(d.Descricao?.Trim(), 30, "descricao");

	public static string? OrigemRecurso(OrigemRecursoDados? d, bool inclusao) =>
		d is null
			? "Informe a origem do recurso."
			: (inclusao ? RegrasCampos.Inteiro(d.CdOrigemRecurso, 1, OrigemMaxima, "cdOrigemRecurso") : null)
				?? RegrasCampos.Obrigatorio(d.Descricao, "descricao")
				?? RegrasCampos.Tamanho(d.Descricao?.Trim(), 50, "descricao")
				?? RegrasCampos.Obrigatorio(d.Tipo, "tipo")
				?? RegrasCampos.Tamanho(d.Tipo?.Trim(), 1, "tipo");

	public static string? Produtor(ProdutorDados? d, bool inclusao) =>
		d is null
			? "Informe o produtor."
			: (inclusao ? RegrasCampos.Inteiro(d.SqProdutor, 1, ProdutorMaximo, "sqProdutor") : null)
				?? RegrasCampos.Obrigatorio(d.Descricao, "descricao")
				?? RegrasCampos.Tamanho(d.Descricao?.Trim(), 80, "descricao")
				?? (d.SqPesCorretor is null ? "O corretor do produtor (sqPesCorretor) é obrigatório." : null)
				?? RegrasCampos.Inteiro(d.SqPesCorretor, 1, 9_999_999, "sqPesCorretor");

	public static string? Indice(IndiceDados? d, bool inclusao)
	{
		if (d is null) return "Informe o índice.";

		if (inclusao)
		{
			if (d.CdCarencia is null) return "A carência (cdCarencia) é obrigatória.";
			if (d.QuantidadeParcelas is null) return "A quantidade de parcelas (quantidadeParcelas) é obrigatória.";

			if ((RegrasCampos.Inteiro(d.CdCarencia, 1, CarenciaMaxima, "cdCarencia")
					?? RegrasCampos.Inteiro(d.QuantidadeParcelas, 1, ParcelasMaximo, "quantidadeParcelas")) is string erroChave)
			{
				return erroChave;
			}
		}

		return d.Indice is decimal i && i > 0 && i <= 9.9999m && decimal.Round(i, 4) == i
			? null
			: "O índice é obrigatório, maior que zero e até 9,9999, com até 4 casas.";
	}

	/// <summary>Texto gravado: sem espaços nas pontas (a caixa alta vem do front, como nas categorias).</summary>
	public static string Texto(string valor) => valor.Trim();

	/// <summary>Código de uma letra (tipo da origem): sem espaços e em maiúscula.</summary>
	public static string Codigo(string valor) => valor.Trim().ToUpperInvariant();
}
