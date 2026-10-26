namespace SigeraAssociados.Application.Cadastros;

/// <summary>
/// Item de uma tabela de apoio (código e descrição), como situação
/// do associado (T000SCLI) e tratamento (T000TRTA).
/// </summary>
public sealed class ItemTabelaDto
{
	public decimal Codigo { get; init; }

	public string Descricao { get; init; } = string.Empty;
}
