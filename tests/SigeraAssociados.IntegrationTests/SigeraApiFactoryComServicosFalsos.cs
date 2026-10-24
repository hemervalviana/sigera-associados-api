using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Usuarios;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Mesma API de testes, com a gestão de usuários e de acessos
/// trocada por versões falsas (sem banco de identidade).
/// </summary>
public sealed class SigeraApiFactoryComServicosFalsos : SigeraApiFactory
{
	public GestaoUsuariosFalso GestaoUsuarios { get; } = new();

	public GestaoAcessosFalso GestaoAcessos { get; } = new();

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);

		builder.ConfigureTestServices(services =>
		{
			services.RemoveAll<IGestaoUsuariosService>();
			services.AddSingleton<IGestaoUsuariosService>(GestaoUsuarios);

			services.RemoveAll<IGestaoAcessosService>();
			services.AddSingleton<IGestaoAcessosService>(GestaoAcessos);
		});
	}
}
