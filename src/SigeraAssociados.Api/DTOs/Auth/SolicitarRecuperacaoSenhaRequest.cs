using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Api.DTOs.Auth;

/// <summary>
/// Representa uma solicitação de recuperação de senha.
/// </summary>
public sealed class SolicitarRecuperacaoSenhaRequest
{
    /// <summary>
    /// Endereço de e-mail associado à conta que deseja recuperar.
    /// </summary>
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um endereço de e-mail válido.")]
    [StringLength(254, ErrorMessage = "O e-mail não pode ultrapassar 254 caracteres.")]
    public string Email { get; init; } = string.Empty;
}
