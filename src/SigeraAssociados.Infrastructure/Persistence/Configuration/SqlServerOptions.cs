
using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Infrastructure.Persistence.Configuration;

/// <summary>
/// Define e valida as opções de conexão com o SQL Server.
/// </summary>
public sealed class SqlServerOptions
{
	public const string SectionName = "ConnectionSettings";

	[Required(ErrorMessage = "O servidor SQL Server é obrigatório.")]
	public string Server { get; set; } = string.Empty;

	[Range(1, 65535, ErrorMessage = "A porta deve estar entre 1 e 65535.")]
	public int Port { get; set; } = 1433;

	[Required(ErrorMessage = "O usuário SQL Server é obrigatório.")]
	public string User { get; set; } = string.Empty;

	[Required(ErrorMessage = "A senha SQL Server é obrigatória.")]
	public string Password { get; set; } = string.Empty;

	public bool Encrypt { get; set; } = true;

	public bool TrustServerCertificate { get; set; } = false;

	/// <summary>
	/// Nível de compatibilidade das bases de negócio (sys.databases.compatibility_level).
	/// As bases PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG estão em 100 (SQL Server 2008).
	/// Com valor abaixo de 130, o EF Core não usa OPENJSON e traduz listas
	/// (filtro de escopo) como IN (...). Atualize se o nível das bases mudar.
	/// </summary>
	[Range(80, 170, ErrorMessage = "O nível de compatibilidade deve estar entre 80 e 170.")]
	public int CompatibilityLevel { get; set; } = 100;
}
