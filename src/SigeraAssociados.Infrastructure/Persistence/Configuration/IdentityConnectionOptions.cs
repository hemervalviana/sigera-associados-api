using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Infrastructure.Persistence.Configuration;

/// <summary>
/// Define as opções de conexão exclusivas do banco central de identidade.
/// As credenciais devem ser fornecidas por configuração segura.
/// </summary>
public sealed class IdentityConnectionOptions
{
    public const string SectionName = "IdentityConnectionSettings";

    [Required(ErrorMessage = "O servidor do banco de identidade é obrigatório.")]
    public string Server { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "A porta do banco de identidade deve estar entre 1 e 65535.")]
    public int Port { get; set; } = 1433;

    [Required(ErrorMessage = "O usuário do banco de identidade é obrigatório.")]
    public string User { get; set; } = string.Empty;

    [Required(ErrorMessage = "A senha do banco de identidade é obrigatória.")]
    public string Password { get; set; } = string.Empty;

    public bool Encrypt { get; set; } = true;

    public bool TrustServerCertificate { get; set; } = false;

    [Required(ErrorMessage = "O nome do banco de identidade é obrigatório.")]
    public string Database { get; set; } = "SigeraIdentity";
}
