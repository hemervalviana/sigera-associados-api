namespace SigeraAssociados.Application.Financeiro;

/// <summary>
/// Conferências de formato comuns aos cadastros financeiros e aos créditos.
/// Cada uma devolve a mensagem de erro, ou null quando o valor serve.
/// </summary>
public static class RegrasCampos
{
	public static string? Obrigatorio(string? valor, string campo) =>
		string.IsNullOrWhiteSpace(valor) ? $"O campo {campo} é obrigatório." : null;

	public static string? Tamanho(string? valor, int maximo, string campo) =>
		valor is not null && valor.Trim().Length > maximo
			? $"O campo {campo} aceita no máximo {maximo} caracteres."
			: null;

	/// <summary>Número inteiro opcional entre mínimo e máximo.</summary>
	public static string? Inteiro(decimal? valor, decimal minimo, decimal maximo, string campo) =>
		valor is not null && (valor < minimo || valor > maximo || valor % 1 != 0)
			? $"O campo {campo} deve ser um número inteiro entre {minimo:0} e {maximo:0}."
			: null;
}
