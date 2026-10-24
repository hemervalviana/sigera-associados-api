namespace SigeraAssociados.Api.DTOs.Auth;

/// <summary>
/// Representa a resposta de uma autenticação bem-sucedida.
/// </summary>
public sealed class LoginResponse
{
    /// <summary>
    /// Token JWT utilizado para autenticar as próximas requisições.
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// Esquema de autenticação utilizado pelo token.
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// Tempo de validade do token, em segundos.
    /// </summary>
    public int ExpiresIn { get; init; }

    /// <summary>
    /// E-mail do usuário autenticado.
    /// </summary>
    public string Email { get; init; } = string.Empty;
}
