using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Api.DTOs.Administracao;

/// <summary>
/// Representa os dados necessários para criar um usuário pela administração.
/// </summary>
public sealed class CriarUsuarioRequest
{
    /// <summary>
    /// Endereço de e-mail utilizado como identificador da conta.
    /// </summary>
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um endereço de e-mail válido.")]
    [StringLength(254, ErrorMessage = "O e-mail não pode ultrapassar 254 caracteres.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Senha inicial da conta, validada também pelo ASP.NET Core Identity.
    /// </summary>
    [Required(ErrorMessage = "A senha é obrigatória.")]
    [StringLength(
        128,
        MinimumLength = 12,
        ErrorMessage = "A senha deve ter entre 12 e 128 caracteres.")]
    public string Senha { get; init; } = string.Empty;

    /// <summary>
    /// Confirmação da senha inicial.
    /// </summary>
    [Required(ErrorMessage = "A confirmação da senha é obrigatória.")]
    [Compare(
        nameof(Senha),
        ErrorMessage = "A senha e a confirmação não coincidem.")]
    public string ConfirmarSenha { get; init; } = string.Empty;

    /// <summary>
    /// Nome do grupo funcional inicial do usuário.
    /// </summary>
    [Required(ErrorMessage = "O grupo inicial é obrigatório.")]
    [StringLength(100, ErrorMessage = "O grupo não pode ultrapassar 100 caracteres.")]
    public string Grupo { get; init; } = string.Empty;
}