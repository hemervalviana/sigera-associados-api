
namespace SigeraAssociados.Infrastructure.Systems;

/// <summary>
/// Armazena o sistema SIGERA selecionado para a requisição atual.
/// Deve ser registrado com ciclo de vida Scoped.
/// </summary>
public sealed class SigeraSystemContext
{
	public int? SystemCode { get; private set; }

	public string? Database { get; private set; }

	/// <summary>
	/// Define o código e o banco associados à requisição atual.
	/// </summary>
	public void SetSystem(int systemCode, string database)
	{
		SystemCode = systemCode;
		Database = database;
	}
}
