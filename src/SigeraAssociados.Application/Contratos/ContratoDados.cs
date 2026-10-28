namespace SigeraAssociados.Application.Contratos;

/// <summary>
/// Dados de inclusão e alteração do contrato (T009CTRT). Na alteração, envie
/// o contrato inteiro: campo opcional omitido fica nulo. Associado, produto,
/// filial e data do contrato não mudam depois da inclusão.
/// </summary>
public sealed class ContratoDados
{
	// ----- Produção 01 -----

	/// <summary>Filial (CD_FIL). Obrigatória na inclusão; na alteração vem da rota.</summary>
	public decimal? CdFil { get; init; }

	/// <summary>Associado (SQ_PES_CLI). Obrigatório; não muda na alteração.</summary>
	public decimal? SqPesCli { get; init; }

	/// <summary>Produto (CD_PRD), de GET /api/cadastros/produtos. Obrigatório; não muda na alteração.</summary>
	public decimal? CdProduto { get; init; }

	/// <summary>Convênio (CD_CVN), de GET /api/convenios. Obrigatório.</summary>
	public decimal? CdConvenio { get; init; }

	/// <summary>Forma de pagamento (TP_FMA_PGT), de GET /api/cadastros/formas-pagamento. Obrigatória.</summary>
	public decimal? CdFormaPagamento { get; init; }

	/// <summary>Situação (CD_ST_CTR), de GET /api/contratos/situacoes. Obrigatória.</summary>
	public decimal? CdSituacao { get; init; }

	/// <summary>
	/// Vínculo do associado com o órgão (SQ_ORG), de GET /api/associados/{sqPesCli}/orgaos.
	/// Obrigatório. O órgão (CD_ORG) vem do vínculo.
	/// </summary>
	public decimal? SqOrgao { get; init; }

	/// <summary>Conta bancária do associado (SQ_CTA_COR), de GET /api/associados/{sqPesCli}/contas. Obrigatória.</summary>
	public decimal? SqContaAssociado { get; init; }

	/// <summary>Município (CD_MUN), de GET /api/cadastros/municipios.</summary>
	public decimal? CdMunicipio { get; init; }

	/// <summary>Data do contrato (DT_CTR). Omitida na inclusão: hoje. Não muda na alteração.</summary>
	public DateTime? DataContrato { get; init; }

	/// <summary>Data da 1ª parcela (DT_PRI_PCL). Obrigatória e posterior à data do contrato.</summary>
	public DateTime? DataPrimeiraParcela { get; init; }

	/// <summary>Valor do contrato (VR_CTR), maior que zero. Obrigatório.</summary>
	public decimal? ValorContrato { get; init; }

	/// <summary>
	/// Quantidade de parcelas (QT_PCL), de 1 a 999. Obrigatória na inclusão: a
	/// API gera essa quantidade de parcelas. Na alteração é ignorada: QT_PCL
	/// acompanha as parcelas (/api/contratos/{cdFil}/{sqCtr}/parcelas).
	/// </summary>
	public decimal? QuantidadeParcelas { get; init; }

	/// <summary>Carência (CD_CAR), de GET /api/contratos/carencias.</summary>
	public decimal? CdCarencia { get; init; }

	/// <summary>
	/// Índice (VR_IND, até 4 casas). Mensalidade = valor do contrato x índice.
	/// Omitido com carência informada: vem de T009INDI (carência e quantidade de parcelas).
	/// </summary>
	public decimal? Indice { get; init; }

	/// <summary>
	/// Mensalidade (VR_MNS). Omitida: valor do contrato x índice. Na inclusão,
	/// precisa resultar maior que zero (é o valor das parcelas geradas).
	/// </summary>
	public decimal? ValorMensalidade { get; init; }

	/// <summary>Cobertura do pecúlio (VR_CBT_PEC), somada à mensalidade em cada parcela gerada.</summary>
	public decimal? ValorCoberturaPeculio { get; init; }

	/// <summary>Consignante (SQ_PES_CCG): outro associado.</summary>
	public decimal? SqConsignante { get; init; }

	/// <summary>Número da proposta (NR_PRP, até 6 dígitos).</summary>
	public decimal? NumeroProposta { get; init; }

	/// <summary>Valor pago por fora (VR_PGT_FORA).</summary>
	public decimal? ValorPagoFora { get; init; }

	/// <summary>Referência de quitação (DT_QIT_CTRT): obrigatória com situação 9 ou 13, e só com elas.</summary>
	public DateTime? DataQuitacao { get; init; }

	/// <summary>Valor da quitação (VR_QIT_CTR).</summary>
	public decimal? ValorQuitacao { get; init; }

	/// <summary>Previsão de quitação (DT_PRV_QIT).</summary>
	public DateTime? DataPrevisaoQuitacao { get; init; }

	/// <summary>Data de finalização (DT_FIN_CTR): obrigatória com situação 12, e só com ela.</summary>
	public DateTime? DataFinalizacao { get; init; }

	/// <summary>Data de cancelamento (DT_CAN_CTRT): obrigatória com situação 10, e só com ela.</summary>
	public DateTime? DataCancelamento { get; init; }

	/// <summary>Valor da compra de dívida (VR_CMP_DIV).</summary>
	public decimal? ValorCompraDivida { get; init; }

	/// <summary>Tipo da compra de dívida (VR_CMP_DIV_TP, até 2 dígitos).</summary>
	public decimal? TipoCompraDivida { get; init; }

	/// <summary>Tipo do contrato (CD_TP_CTRT, até 2 dígitos).</summary>
	public decimal? CdTipoContrato { get; init; }

	/// <summary>Tarifa bancária (VR_TAR_BCO, até 99,99).</summary>
	public decimal? ValorTarifaBancaria { get; init; }

	/// <summary>Despesa administrativa (VR_DSP_ADM).</summary>
	public decimal? ValorDespesaAdministrativa { get; init; }

	/// <summary>VR_INA.</summary>
	public decimal? ValorIna { get; init; }

	/// <summary>Observação (OB_CTR, até 2000).</summary>
	public string? Observacao { get; init; }

	/// <summary>OB_SNT (até 255).</summary>
	public string? ObservacaoSnt { get; init; }

	// ----- Produção 02 -----

	/// <summary>
	/// Corretor (SQ_PES_CRO), de GET /api/corretores. Obrigatório, salvo para
	/// quem só tem acesso pela carteira de um corretor (aí é esse corretor).
	/// </summary>
	public decimal? SqPesCorretor { get; init; }

	/// <summary>Conta bancária do corretor (SQ_CTA_COR_CRRT), de GET /api/corretores/{sq}/contas.</summary>
	public decimal? SqContaCorretor { get; init; }

	/// <summary>Categoria (CD_CAT, até 3 dígitos). Obrigatória.</summary>
	public decimal? CdCategoria { get; init; }

	/// <summary>Origem do recurso (CD_ORI_REC), de GET /api/contratos/origens-recurso.</summary>
	public decimal? CdOrigemRecurso { get; init; }

	/// <summary>Produtor (SQ_PRD), de GET /api/contratos/produtores?sqPesCorretor=.</summary>
	public decimal? SqProdutor { get; init; }

	/// <summary>Comissão (VR_CMS).</summary>
	public decimal? ValorComissao { get; init; }

	/// <summary>Pagamento da comissão (DT_PGT_CMS).</summary>
	public DateTime? DataPagamentoComissao { get; init; }

	/// <summary>Pagamento do auxílio (DT_PGT_AUX).</summary>
	public DateTime? DataPagamentoAuxilio { get; init; }

	/// <summary>Valor do depósito (VR_DEP).</summary>
	public decimal? ValorDeposito { get; init; }

	/// <summary>Banco do depósito (CD_BCO), de GET /api/cadastros/bancos.</summary>
	public decimal? CdBanco { get; init; }

	/// <summary>CD_ENT_FISI (1 dígito).</summary>
	public decimal? CdEntFisi { get; init; }
}

/// <summary>Contrato com as descrições, o resumo e as parcelas.</summary>
public sealed class ContratoDto
{
	public decimal CdFil { get; init; }
	public decimal SqCtr { get; init; }
	public decimal SqPesCli { get; init; }
	public string? NomeAssociado { get; init; }
	public string? CpfAssociado { get; init; }
	public decimal? CdProduto { get; init; }
	public string? Produto { get; init; }
	public decimal? CdConvenio { get; init; }
	public string? Convenio { get; init; }
	public decimal CdFormaPagamento { get; init; }
	public string? FormaPagamento { get; init; }
	public decimal? CdSituacao { get; init; }
	public string? Situacao { get; init; }
	public decimal? SqOrgao { get; init; }
	public decimal? CdOrgao { get; init; }
	public string? Orgao { get; init; }
	public decimal? SqContaAssociado { get; init; }
	public decimal? CdMunicipio { get; init; }
	public string? Municipio { get; init; }
	public DateTime DataContrato { get; init; }
	public DateTime? DataPrimeiraParcela { get; init; }
	public decimal? ValorContrato { get; init; }
	public decimal? QuantidadeParcelas { get; init; }
	public decimal? CdCarencia { get; init; }
	public string? Carencia { get; init; }
	public decimal? Indice { get; init; }
	public decimal? ValorMensalidade { get; init; }
	public decimal? ValorCoberturaPeculio { get; init; }
	public decimal? SqConsignante { get; init; }
	public string? Consignante { get; init; }
	public decimal? NumeroProposta { get; init; }
	public decimal? ValorPagoFora { get; init; }
	public DateTime? DataQuitacao { get; init; }
	public decimal? ValorQuitacao { get; init; }
	public DateTime? DataPrevisaoQuitacao { get; init; }
	public DateTime? DataFinalizacao { get; init; }
	public DateTime? DataCancelamento { get; init; }

	/// <summary>Encerramento (DT_ENC_CTR): preenchido pela API quando a situação passa a 4 ou 9.</summary>
	public DateTime? DataEncerramento { get; init; }

	/// <summary>Último desconto (DT_ULT_DCT): vencimento da última parcela, no produto 2.</summary>
	public DateTime? DataUltimoDesconto { get; init; }

	public decimal? ValorCompraDivida { get; init; }
	public decimal? TipoCompraDivida { get; init; }
	public decimal? CdTipoContrato { get; init; }
	public decimal? ValorTarifaBancaria { get; init; }
	public decimal? ValorDespesaAdministrativa { get; init; }
	public decimal? ValorIna { get; init; }
	public string? Observacao { get; init; }
	public string? ObservacaoSnt { get; init; }
	public decimal SqPesCorretor { get; init; }
	public string? Corretor { get; init; }
	public decimal? SqContaCorretor { get; init; }
	public decimal? CdCategoria { get; init; }
	public decimal? CdOrigemRecurso { get; init; }
	public string? OrigemRecurso { get; init; }
	public decimal? SqProdutor { get; init; }
	public string? Produtor { get; init; }
	public decimal? ValorComissao { get; init; }
	public DateTime? DataPagamentoComissao { get; init; }
	public DateTime? DataPagamentoAuxilio { get; init; }
	public decimal? ValorDeposito { get; init; }
	public decimal? CdBanco { get; init; }
	public string? Banco { get; init; }
	public decimal? CdEntFisi { get; init; }

	/// <summary>Cadastro (DT_CAD) e usuário que incluiu (NM_USU).</summary>
	public DateTime DataCadastro { get; init; }
	public string? UsuarioCadastro { get; init; }

	public ResumoParcelas Parcelas { get; init; } = new();
}

/// <summary>Totais das parcelas do contrato e a lista.</summary>
public sealed class ResumoParcelas
{
	public int Quantidade { get; init; }
	public int Baixadas { get; init; }
	public decimal TotalParcelas { get; init; }
	public decimal TotalPago { get; init; }
	public IReadOnlyList<ParcelaDto> Itens { get; init; } = [];
}

/// <summary>Linha da pesquisa de contratos.</summary>
public sealed class ContratoResumoDto
{
	public decimal CdFil { get; init; }
	public decimal SqCtr { get; init; }
	public decimal SqPesCli { get; init; }
	public string? NomeAssociado { get; init; }
	public string? CpfAssociado { get; init; }
	public decimal SqPesCorretor { get; init; }
	public string? Corretor { get; init; }
	public decimal? CdProduto { get; init; }
	public string? Produto { get; init; }
	public decimal? CdSituacao { get; init; }
	public string? Situacao { get; init; }
	public DateTime DataContrato { get; init; }
	public DateTime? DataPrimeiraParcela { get; init; }
	public decimal? ValorContrato { get; init; }
	public decimal? ValorMensalidade { get; init; }
	public decimal? QuantidadeParcelas { get; init; }
}

public sealed class PesquisaContratosRequest
{
	/// <summary>Número do contrato, CPF do associado ou começo do nome do associado.</summary>
	public string? Busca { get; init; }
	public decimal? SqPesCli { get; init; }
	public decimal? CdFil { get; init; }
	public decimal? CdSituacao { get; init; }
	public decimal? SqPesCorretor { get; init; }
	public decimal? CdProduto { get; init; }
	public int Pagina { get; init; } = 1;
	public int TamanhoPagina { get; init; } = 50;
}

public sealed class PesquisaContratosResultado
{
	public int Pagina { get; init; }
	public int TamanhoPagina { get; init; }
	public int TotalRegistros { get; init; }
	public int TotalPaginas { get; init; }
	public IReadOnlyList<ContratoResumoDto> Contratos { get; init; } = [];
}

/// <summary>
/// Inclusão e alteração de parcela: só vencimento e valor. Os campos de
/// pagamento (baixa) são gravados pelas rotinas do legado.
/// </summary>
public sealed class ParcelaDados
{
	/// <summary>Vencimento (DT_VEN). Obrigatório.</summary>
	public DateTime? DataVencimento { get; init; }

	/// <summary>Valor (VR_PCL), maior que zero. Obrigatório.</summary>
	public decimal? Valor { get; init; }
}

/// <summary>Parcela do contrato (T009PAGA).</summary>
public sealed class ParcelaDto
{
	public decimal NumeroParcela { get; init; }
	public DateTime DataVencimento { get; init; }
	public decimal Valor { get; init; }
	public DateTime? DataPagamento { get; init; }
	public decimal? ValorPago { get; init; }
	public decimal? ValorMulta { get; init; }
	public decimal? ValorJuros { get; init; }
	public decimal? ValorDesconto { get; init; }
	public decimal? Saldo { get; init; }
	public decimal? NumeroCheque { get; init; }
	public bool? Remessa { get; init; }
	public decimal? TipoBaixa { get; init; }
	public decimal? CdFormaPagamento { get; init; }
	public decimal? CdConvenio { get; init; }
	public decimal? CdAverbacao { get; init; }
	public string? IdCobranca { get; init; }
	public DateTime? DataGeracao { get; init; }
	public string? Usuario { get; init; }

	/// <summary>
	/// True quando a parcela já foi paga, averbada ou foi para remessa: aí não
	/// pode ser alterada nem excluída pela API.
	/// </summary>
	public bool Baixada { get; init; }
}

/// <summary>Item de tabela de apoio do contrato.</summary>
public sealed record ApoioContratoDto(decimal Codigo, string Descricao, string? Tipo = null);

/// <summary>Índice de T009INDI.</summary>
public sealed record IndiceContratoDto(decimal CdCarencia, decimal QuantidadeParcelas, decimal Indice);
