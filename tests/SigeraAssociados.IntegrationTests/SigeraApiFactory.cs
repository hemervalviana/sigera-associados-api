using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Cria uma instância isolada da API para os testes de integração.
/// Utiliza configurações JWT fictícias e autenticação simulada.
/// </summary>
public class SigeraApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Configura o ambiente isolado utilizado pelos testes.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Define configurações fictícias exclusivamente para os testes.
        // A chave possui pelo menos 32 bytes e é armazenada em Base64.
        builder.UseSetting("Jwt:Issuer", "SigeraAssociados.Api");
        builder.UseSetting("Jwt:Audience", "SigeraAssociados.Clients");
        builder.UseSetting(
            "Jwt:SigningKey",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(
                    "SigeraIntegrationTestKey2026XYZ!")));
        builder.UseSetting("Jwt:AccessTokenMinutes", "15");

        // Conexões fictícias: as opções são validadas na inicialização
        // (ValidateOnStart) e o ambiente Testing não carrega User Secrets.
        // Nenhum teste deve chegar a abrir conexão com esses bancos.
        builder.UseSetting("ConnectionSettings:Server", "127.0.0.1");
        builder.UseSetting("ConnectionSettings:User", "usuario_teste");
        builder.UseSetting("ConnectionSettings:Password", "senha_teste");
        builder.UseSetting("IdentityConnectionSettings:Server", "127.0.0.1");
        builder.UseSetting("IdentityConnectionSettings:User", "usuario_teste");
        builder.UseSetting("IdentityConnectionSettings:Password", "senha_teste");
        builder.UseSetting("IdentityConnectionSettings:Database", "SigeraIdentityTeste");
        builder.UseSetting("SeedAdmin:Habilitado", "false");

        // Substitui a autenticação real por um esquema exclusivo de testes.
        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme =
                        TesteAutenticacaoHandler.Esquema;

                    options.DefaultChallengeScheme =
                        TesteAutenticacaoHandler.Esquema;

                    options.DefaultScheme =
                        TesteAutenticacaoHandler.Esquema;
                })
                .AddScheme<
                    AuthenticationSchemeOptions,
                    TesteAutenticacaoHandler>(
                    TesteAutenticacaoHandler.Esquema,
                    _ => { });
        });
    }
}
