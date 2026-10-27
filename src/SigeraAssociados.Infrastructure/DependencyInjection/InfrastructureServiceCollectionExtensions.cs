using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Associados;
using SigeraAssociados.Application.Interfaces.Cadastros;
using SigeraAssociados.Application.Interfaces.Orgaos;
using SigeraAssociados.Application.Interfaces.Enderecos;
using SigeraAssociados.Application.Interfaces.ContasBancarias;
using SigeraAssociados.Application.Interfaces.Telefones;
using SigeraAssociados.Application.Interfaces.Pix;
using SigeraAssociados.Application.Interfaces.Referencias;
using SigeraAssociados.Application.Interfaces.Financeiro;
using SigeraAssociados.Application.Interfaces.Creditos;
using SigeraAssociados.Application.Interfaces.Repositories;
using SigeraAssociados.Application.Interfaces.Usuarios;
using SigeraAssociados.Infrastructure.Acesso;
using SigeraAssociados.Infrastructure.Associados;
using SigeraAssociados.Infrastructure.Cadastros;
using SigeraAssociados.Infrastructure.Orgaos;
using SigeraAssociados.Infrastructure.Enderecos;
using SigeraAssociados.Infrastructure.ContasBancarias;
using SigeraAssociados.Infrastructure.Telefones;
using SigeraAssociados.Infrastructure.Pix;
using SigeraAssociados.Infrastructure.Referencias;
using SigeraAssociados.Infrastructure.Financeiro;
using SigeraAssociados.Infrastructure.Creditos;
using SigeraAssociados.Infrastructure.Persistence.Configuration;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Persistence.Identity;
using SigeraAssociados.Infrastructure.Repositories;
using SigeraAssociados.Infrastructure.Systems;
using SigeraAssociados.Infrastructure.Usuarios;

namespace SigeraAssociados.Infrastructure.DependencyInjection;

/// <summary>
/// Registra os serviços de infraestrutura da API SIGERA.
/// Mantém separados o acesso aos bancos de negócio e a identidade.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddSigeraInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configura e valida a conexão utilizada pelos quatro sistemas SIGERA.
        services.AddOptions<SqlServerOptions>()
            .Bind(configuration.GetSection(SqlServerOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Server),
                "O servidor SQL Server deve ser informado.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.User),
                "O usuário SQL Server deve ser informado.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Password),
                "A senha SQL Server deve ser informada.")
            .ValidateOnStart();

        // Configura separadamente a conexão do banco central de identidade.
        services.AddOptions<IdentityConnectionOptions>()
            .Bind(configuration.GetSection(IdentityConnectionOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Server),
                "O servidor do banco de identidade deve ser informado.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.User),
                "O usuário do banco de identidade deve ser informado.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Password),
                "A senha do banco de identidade deve ser informada.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Database),
                "O nome do banco de identidade deve ser informado.")
            .ValidateOnStart();

        // Registra o contexto exclusivo da identidade centralizada.
        services.AddDbContext<SigeraIdentityDbContext>(
            (serviceProvider, dbOptions) =>
            {
                var identityOptions = serviceProvider
                    .GetRequiredService<IOptions<IdentityConnectionOptions>>()
                    .Value;

                var connectionStringBuilder = new SqlConnectionStringBuilder
                {
                    DataSource = $"{identityOptions.Server},{identityOptions.Port}",
                    InitialCatalog = identityOptions.Database,
                    UserID = identityOptions.User,
                    Password = identityOptions.Password,
                    Encrypt = identityOptions.Encrypt,
                    TrustServerCertificate = identityOptions.TrustServerCertificate
                };

                dbOptions.UseSqlServer(connectionStringBuilder.ConnectionString);
            });

        // Registra o ASP.NET Core Identity com usuários e funções.
        // Os dados serão persistidos exclusivamente em SigeraIdentityDbContext.
        services.AddDataProtection();
        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan =
                    TimeSpan.FromMinutes(15);

                options.SignIn.RequireConfirmedEmail = true;
            })       
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<SigeraIdentityDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // Mantém o mecanismo existente de seleção dos bancos de negócio.
        services.AddSingleton<SigeraSystemResolver>();
        services.AddSingleton<SigeraDbContextFactory>();
        services.AddScoped<IAssociadosRepository, AssociadosRepository>();

        // Escopos de acesso (sistema, empresa, filial e carteira do corretor).
        // IUsuarioAtual é registrado pela camada Api.
        services.AddMemoryCache();
        services.AddScoped<IEscopoAcessoService, EscopoAcessoService>();
        services.AddScoped<IGestaoAcessosService, GestaoAcessosService>();

        // Confere empresa, filial e corretor no banco do sistema ao conceder acesso.
        services.AddScoped<IVerificadorCadastroSistema, VerificadorCadastroSistema>();
        services.AddScoped<ICadastrosSistemaService, CadastrosSistemaService>();
        services.AddScoped<IManutencaoTabelasApoioService, ManutencaoTabelasApoioService>();
        services.AddScoped<IOrgaosService, OrgaosService>();
        services.AddScoped<IVinculosOrgaoService, VinculosOrgaoService>();
        services.AddScoped<IEnderecosAssociadoService, EnderecosAssociadoService>();
        services.AddScoped<ITelefonesAssociadoService, TelefonesAssociadoService>();
        services.AddScoped<IChavesPixService, ChavesPixService>();
        services.AddScoped<IReferenciasAssociadoService, ReferenciasAssociadoService>();
        services.AddScoped<ICadastrosFinanceirosService, CadastrosFinanceirosService>();
        services.AddScoped<ICreditosAssociadoService, CreditosAssociadoService>();
        services.AddScoped<IContasBancariasService, ContasBancariasService>();
        services.AddScoped<ICadastroAssociadosService, CadastroAssociadosService>();

        // Consulta e manutenção das contas de usuário.
        services.AddScoped<IGestaoUsuariosService, GestaoUsuariosService>();

        // Sessões: tokens de renovação, carimbo de segurança e logout.
        services.AddOptions<OpcoesSessao>()
            .Bind(configuration.GetSection(OpcoesSessao.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ITokensRenovacaoService, TokensRenovacaoService>();
        services.AddScoped<ISessoesService, SessoesService>();

        return services;
    }
}



