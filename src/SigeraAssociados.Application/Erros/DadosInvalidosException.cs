namespace SigeraAssociados.Application.Erros;

/// <summary>
/// Dados de entrada inválidos (filtros, paginação, ordenação, grupos...).
/// A API converte esta exceção em resposta 400 (ProblemDetails).
/// Use somente para erros do consumidor, nunca para falhas internas.
/// </summary>
public sealed class DadosInvalidosException : Exception
{
	public DadosInvalidosException(
		string mensagem,
		IReadOnlyDictionary<string, object?>? detalhes = null)
		: base(mensagem)
	{
		Detalhes = detalhes ?? new Dictionary<string, object?>();
	}

	/// <summary>
	/// Informações extras incluídas na resposta (ex.: camposPermitidos).
	/// </summary>
	public IReadOnlyDictionary<string, object?> Detalhes { get; }
}
