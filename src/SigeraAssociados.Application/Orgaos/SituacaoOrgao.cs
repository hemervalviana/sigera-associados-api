namespace SigeraAssociados.Application.Orgaos;

/// <summary>
/// Situação do órgão (T000ORGA.CD_SIT_ORG): 1 = ATIVO, 0 = INATIVO.
/// Atalho para SituacaoAtivoInativo.Orgao. Não confundir com a situação
/// do vínculo do associado com o órgão (T000RPOR.CD_SIT_ORG, tabela T000SPOG).
/// </summary>
public static class SituacaoOrgao
{
	public const decimal Inativo = 0;
	public const decimal Ativo = 1;

	public static string? Descricao(decimal? codigo) => SituacaoAtivoInativo.Orgao.Descricao(codigo);

	public static bool Valida(decimal? codigo) => SituacaoAtivoInativo.Orgao.Valida(codigo);
}
