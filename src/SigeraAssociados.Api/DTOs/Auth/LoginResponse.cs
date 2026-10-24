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
    /// Token de renovação: use em POST /api/auth/renovar para obter
    /// um novo token de acesso sem informar a senha. É de uso único:
    /// cada renovação devolve um novo refreshToken.
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>
    /// Data e hora (UTC) em que o refreshToken expira.
    /// </summary>
    public DateTimeOffset RefreshTokenExpiraEm { get; init; }

    /// <summary>
    /// E-mail do usuário autenticado.
    /// </summary>
    public string Email { get; init; } = string.Empty;
}
