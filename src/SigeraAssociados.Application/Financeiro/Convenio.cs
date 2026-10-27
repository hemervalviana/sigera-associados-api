namespace SigeraAssociados.Application.Financeiro;

/// <summary>
/// Convênio (T009CONV), com todos os campos da tabela.
/// Na alteração, o código vem da rota.
/// </summary>
public sealed class ConvenioDados
{
	/// <summary>Código (CD_CVN, 1 a 999). Na inclusão, omitir para usar MAX + 1.</summary>
	public decimal? CdCvn { get; init; }

	/// <summary>Nome (NM_CVN, até 35). Obrigatório.</summary>
	/// <example>CONSIGNADO SEDUC</example>
	public string? Nome { get; init; }

	/// <summary>Número do convênio (NR_CVN, até 15).</summary>
	public string? Numero { get; init; }

	/// <summary>Segundo número (NR_CV2, até 15).</summary>
	public string? Numero2 { get; init; }

	/// <summary>Terceiro número (NR_CV3, até 15).</summary>
	public string? Numero3 { get; init; }

	/// <summary>Filial (CD_FIL), de GET /api/cadastros/filiais.</summary>
	public decimal? CdFil { get; init; }

	/// <summary>
	/// Banco (CD_BCO), de GET /api/cadastros/bancos. Opcional; não pode ser
	/// informado junto com tipoGoverno (o convênio é de banco ou de governo).
	/// </summary>
	public decimal? CdBanco { get; init; }

	/// <summary>Conta corrente da empresa (SQ_CTA_COR), conferida em T009CONT.</summary>
	public decimal? SqContaCorrente { get; init; }

	/// <summary>Código da empresa no convênio (CD_EMPR, até 20 dígitos).</summary>
	public decimal? CodigoEmpresa { get; init; }

	/// <summary>Carteira (CD_CART, até 3 dígitos).</summary>
	public decimal? Carteira { get; init; }

	/// <summary>Código de compensação (CD_COMP, até 4 dígitos).</summary>
	public decimal? CodigoCompensacao { get; init; }

	/// <summary>Código CNAB (CD_CNAB, até 18 dígitos).</summary>
	public decimal? CodigoCnab { get; init; }

	/// <summary>Parâmetro de transmissão (PAR_TRSM, até 2 dígitos).</summary>
	public decimal? ParametroTransmissao { get; init; }

	/// <summary>
	/// Tipo de governo (CT_TP_GOV), de GET /api/convenios/tipos-governo
	/// (valores fixos do legado, 1 a 20). Opcional; não pode ser informado
	/// junto com cdBanco.
	/// </summary>
	public decimal? TipoGoverno { get; init; }

	/// <summary>Descrição do tipo de governo. Só na resposta.</summary>
	public string? DescricaoTipoGoverno { get; init; }

	/// <summary>Diretório dos arquivos de remessa (NM_DRT_REM, até 40).</summary>
	public string? DiretorioRemessa { get; init; }

	/// <summary>Diretório dos arquivos de retorno (NM_DRT_ROR, até 40).</summary>
	public string? DiretorioRetorno { get; init; }

	/// <summary>Extensão dos arquivos (DE_EXT_ARQ, até 5).</summary>
	public string? ExtensaoArquivo { get; init; }

	/// <summary>Fonte de dados ODBC (DE_ODBC, até 100).</summary>
	public string? Odbc { get; init; }

	/// <summary>Situação (CD_SIT_CON, 0 a 9, valores do legado).</summary>
	public decimal? CdSituacao { get; init; }
}

/// <summary>
/// Convênio na pesquisa.
/// </summary>
public sealed class ConvenioResumoDto
{
	public decimal CdCvn { get; init; }

	public string Nome { get; init; } = string.Empty;

	public string? Numero { get; init; }

	public decimal? CdFil { get; init; }

	public decimal? CdBanco { get; init; }

	public decimal? TipoGoverno { get; init; }

	public string? DescricaoTipoGoverno { get; init; }

	public decimal? CdSituacao { get; init; }
}

public static class ValidacaoConvenio
{
	public const decimal CodigoMaximo = 999;

	/// <summary>
	/// Formato dos campos e a regra do vínculo: o convênio é de banco ou de
	/// governo, nunca dos dois (cdBanco e tipoGoverno juntos). Os dois
	/// vazios são aceitos.
	/// </summary>
	public static string? Validar(ConvenioDados? dados, bool inclusao)
	{
		if (dados is null)
		{
			return "Informe os dados do convênio.";
		}

		return Campos(dados, inclusao) ?? Vinculo(dados);
	}

	/// <summary>Banco e governo juntos são recusados; um só ou nenhum, aceitos.</summary>
	public static string? Vinculo(ConvenioDados dados) =>
		dados.CdBanco is not null && dados.TipoGoverno is not null
			? "O convênio é de banco ou de governo: informe cdBanco ou tipoGoverno, não os dois."
			: null;

	private static string? Campos(ConvenioDados dados, bool inclusao)
	{
		return (inclusao ? RegrasCampos.Inteiro(dados.CdCvn, 1, CodigoMaximo, "cdCvn") : null)
			?? RegrasCampos.Obrigatorio(dados.Nome, "nome")
			?? RegrasCampos.Tamanho(dados.Nome, 35, "nome")
			?? RegrasCampos.Tamanho(dados.Numero, 15, "numero")
			?? RegrasCampos.Tamanho(dados.Numero2, 15, "numero2")
			?? RegrasCampos.Tamanho(dados.Numero3, 15, "numero3")
			?? RegrasCampos.Inteiro(dados.CdFil, 1, 99, "cdFil")
			?? RegrasCampos.Inteiro(dados.CdBanco, 1, 999, "cdBanco")
			?? RegrasCampos.Inteiro(dados.SqContaCorrente, 1, 999, "sqContaCorrente")
			?? RegrasCampos.Inteiro(dados.CodigoEmpresa, 0, 99_999_999_999_999_999_999m, "codigoEmpresa")
			?? RegrasCampos.Inteiro(dados.Carteira, 0, 999, "carteira")
			?? RegrasCampos.Inteiro(dados.CodigoCompensacao, 0, 9_999, "codigoCompensacao")
			?? RegrasCampos.Inteiro(dados.CodigoCnab, 0, 999_999_999_999_999_999m, "codigoCnab")
			?? RegrasCampos.Inteiro(dados.ParametroTransmissao, 0, 99, "parametroTransmissao")
			?? (TipoGovernoConvenio.Valido(dados.TipoGoverno)
				? null
				: "O campo tipoGoverno deve ser um dos códigos de GET /api/convenios/tipos-governo (1 a 20).")
			?? RegrasCampos.Tamanho(dados.DiretorioRemessa, 40, "diretorioRemessa")
			?? RegrasCampos.Tamanho(dados.DiretorioRetorno, 40, "diretorioRetorno")
			?? RegrasCampos.Tamanho(dados.ExtensaoArquivo, 5, "extensaoArquivo")
			?? RegrasCampos.Tamanho(dados.Odbc, 100, "odbc")
			?? RegrasCampos.Inteiro(dados.CdSituacao, 0, 9, "cdSituacao");
	}
}
