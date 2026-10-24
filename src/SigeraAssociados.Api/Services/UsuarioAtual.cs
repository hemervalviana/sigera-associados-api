using System.Security.Claims;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api.Services;

/// <summary>
/// Expõe o usuário autenticado da requisição HTTP atual.
/// </summary>
public sealed class UsuarioAtual : IUsuarioAtual
{
	private readonly IHttpContextAccessor _httpContextAccessor;

	public UsuarioAtual(IHttpContextAccessor httpContextAccessor)
	{
		_httpContextAccessor = httpContextAccessor;
	}

	private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

	/// <inheritdoc />
	public string? UsuarioId =>
		Principal?.Identity?.IsAuthenticated == true
			? Principal.FindFirstValue(ClaimTypes.NameIdentifier)
			: null;

	/// <inheritdoc />
	public bool PossuiAcessoIrrestrito =>
		Principal?.Identity?.IsAuthenticated == true
		&& Grupos.AcessoIrrestrito.Any(Principal.IsInRole);
}
