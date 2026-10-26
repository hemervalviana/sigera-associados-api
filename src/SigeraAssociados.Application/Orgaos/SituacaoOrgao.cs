namespace SigeraAssociados.Application.Orgaos;

/// <summary>
/// Situação do órgão (T000ORGA.CD_SIT_ORG): valores fixos do sistema
/// legado, sem tabela no banco. Não confundir com a situação do vínculo
/// do associado com o órgão (T000RPOR.CD_SIT_ORG, tabela T000SPOG).
/// </summary>
public static class SituacaoOrgao
{
	public const decimal Inativo = 0;
	public const decimal Ativo = 1;

	/// <summary>
	/// Descrição do código: ATIVO, INATIVO ou null (sem situação ou código desconhecido).
	/// </summary>
	public static string? Descricao(decimal? codigo) => codigo switch
	{
		Ativo => "ATIVO",
		Inativo => "INATIVO",
		_ => null
	};

	public static bool Valida(decimal? codigo) => codigo is null or Ativo or Inativo;
}
