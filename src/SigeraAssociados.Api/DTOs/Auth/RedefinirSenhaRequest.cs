using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Api.DTOs.Auth;

/// <summary>
/// Representa os dados necessários para redefinir a senha de um usuário.
/// </summary>
public sealed class RedefinirSenhaRequest
{
    /// <summary>
    /// Endereço de e-mail associado à conta.
    /// </summary>
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um endereço de e-mail válido.")]
    [StringLength(254, ErrorMessage = "O e-mail não pode ultrapassar 254 caracteres.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Token de redefinição gerado pelo ASP.NET Core Identity.
    /// </summary>
    [Required(ErrorMessage = "O token de redefinição é obrigatório.")]
    [StringLength(4096, ErrorMessage = "O token de redefinição é muito longo.")]
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Nova senha escolhida pelo usuário.
    /// </summary>
    [Required(ErrorMessage = "A nova senha é obrigatória.")]
    [StringLength(
        128,
        MinimumLength = 12,
        ErrorMessage = "A senha deve ter entre 12 e 128 caracteres.")]
    public string NovaSenha { get; init; } = string.Empty;

    /// <summary>
    /// Confirmação da nova senha.
    /// </summary>
    [Required(ErrorMessage = "A confirmação da nova senha é obrigatória.")]
    [Compare(
        nameof(NovaSenha),
        ErrorMessage = "A nova senha e a confirmação não coincidem.")]
    public string ConfirmarNovaSenha { get; init; } = string.Empty;
}
