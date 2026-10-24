using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Api.DTOs.Auth;

/// <summary>
/// Representa os dados necessários para confirmar o e-mail de um usuário.
/// </summary>
public sealed class ConfirmarEmailRequest
{
    /// <summary>
    /// Endereço de e-mail informado durante o cadastro.
    /// </summary>
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um endereço de e-mail válido.")]
    [StringLength(254, ErrorMessage = "O e-mail não pode ultrapassar 254 caracteres.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Token de confirmação gerado pelo ASP.NET Core Identity.
    /// </summary>
    [Required(ErrorMessage = "O token de confirmação é obrigatório.")]
    [StringLength(4096, ErrorMessage = "O token de confirmação é muito longo.")]
    public string Token { get; init; } = string.Empty;
}
