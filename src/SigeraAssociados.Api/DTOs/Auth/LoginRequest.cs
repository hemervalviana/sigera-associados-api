using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Api.DTOs.Auth;

/// <summary>
/// Representa os dados recebidos para autenticar um usuário.
/// </summary>
public sealed class LoginRequest
{
    /// <summary>
    /// Endereço de e-mail cadastrado no sistema.
    /// </summary>
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um endereço de e-mail válido.")]
    [StringLength(254, ErrorMessage = "O e-mail não pode ultrapassar 254 caracteres.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Senha informada pelo usuário.
    /// A validação da senha será realizada pelo ASP.NET Core Identity.
    /// </summary>
    [Required(ErrorMessage = "A senha é obrigatória.")]
    [StringLength(
        128,
        MinimumLength = 1,
        ErrorMessage = "A senha deve ter no máximo 128 caracteres.")]
    public string Senha { get; init; } = string.Empty;
}
