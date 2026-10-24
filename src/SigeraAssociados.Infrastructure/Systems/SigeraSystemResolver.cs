
namespace SigeraAssociados.Infrastructure.Systems;

public sealed class SigeraSystemResolver
{
	public string ResolveDatabase(int code)
	{
		if (!SigeraSystemCatalog.TryGetDatabase(code, out var database))
		{
			throw new ArgumentOutOfRangeException(
				nameof(code),
				code,
				"Código de sistema inválido. Os valores permitidos são 1, 2, 3 e 4.");
		}

		return database;
	}
}
