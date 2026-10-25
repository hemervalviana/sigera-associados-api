using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.OpenApi;
using SigeraAssociados.Api;
using SigeraAssociados.Api.Erros;
using SigeraAssociados.Api.Middlewares;
using SigeraAssociados.Api.Saude;
using SigeraAssociados.Api.Seguranca;
using SigeraAssociados.Api.Services;
using SigeraAssociados.Api.Swagger;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Associados;
using SigeraAssociados.Infrastructure.DependencyInjection;
using SigeraAssociados.Infrastructure.Systems;
using System.Text;

// Cria o construtor da aplicação ASP.NET Core.
// A configuração carrega appsettings.json, User Secrets
// em ambiente de desenvolvimento e variáveis de ambiente.
// Como serviço do Windows, a pasta de trabalho é C:\Windows\System32;
// a pasta de conteúdo passa a ser a do executável, para achar os appsettings.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
	Args = args,
	ContentRootPath = WindowsServiceHelpers.IsWindowsService()
		? AppContext.BaseDirectory
		: default
});

// Permite rodar como serviço do Windows (produção). Fora de um serviço,
// como no dotnet run, não muda nada. Os logs vão também para o
// Visualizador de Eventos do Windows (log Aplicativo).
builder.Services.AddWindowsService(options =>
{
	options.ServiceName = "SigeraAssociadosApi";
});

// URL pública do cloudflared tunnel (chave UrlPublica:BaseUrl).
// O arquivo é criado e apagado pelo script scripts/iniciar-tunel-cloudflared.ps1
// e recarregado automaticamente: não é preciso reiniciar a API.
builder.Configuration.AddJsonFile(
	"urlpublica.local.json",
	optional: true,
	reloadOnChange: true);

// Obtém as configurações públicas utilizadas na validação JWT.
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
var jwtAccessTokenMinutes = builder.Configuration.GetValue<int?>("Jwt:AccessTokenMinutes");

// Valida as configurações essenciais antes de iniciar a API.
if (string.IsNullOrWhiteSpace(jwtIssuer))
{
	throw new InvalidOperationException(
	"A configuração Jwt:Issuer não foi definida.");
}

if (string.IsNullOrWhiteSpace(jwtAudience))
{
	throw new InvalidOperationException(
	"A configuração Jwt:Audience não foi definida.");
}

if (string.IsNullOrWhiteSpace(jwtSigningKey))
{
	throw new InvalidOperationException(
	"A chave JWT não foi configurada. Configure Jwt:SigningKey nos User Secrets ou em um provedor seguro de segredos.");
}

if (jwtAccessTokenMinutes is null or < 1 or > 1440)
{
	throw new InvalidOperationException(
	"Jwt:AccessTokenMinutes deve estar entre 1 e 1440 minutos.");
}

// A chave foi gerada em Base64 e deve conter pelo menos
// 256 bits de material criptográfico para assinatura HMAC.
byte[] jwtKeyBytes;

try
{
	jwtKeyBytes = Convert.FromBase64String(jwtSigningKey);
}
catch (FormatException ex)
{
	throw new InvalidOperationException(
	"Jwt:SigningKey deve conter uma chave válida em Base64.", ex);
}

if (jwtKeyBytes.Length < 32)
{
	throw new InvalidOperationException(
	"A chave JWT deve conter pelo menos 32 bytes após a decodificação Base64.");
}

// Registra suporte a controllers da API REST.
builder.Services.AddControllers();

// Formato único de erro: ProblemDetails (application/problem+json)
// com título em português, codigo e traceId.
builder.Services.AddProblemDetails(options =>
{
	options.CustomizeProblemDetails = contexto =>
		RespostasErro.Completar(contexto.ProblemDetails, contexto.HttpContext);
});

// Converte exceções em ProblemDetails e registra no log as falhas não previstas.
builder.Services.AddExceptionHandler<TratadorExcecoes>();

// Registra o Swagger (Swashbuckle): documento e tela de testes.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "SigeraAssociados API",
		Version = "v1",
		Description =
			"Consulta de associados dos sistemas PROASP, SONASP, ABSP e GSP. " +
			"Faça login em POST /api/auth/login, clique em Authorize e informe o token."
	});

	// Botão Authorize: envia o token JWT no cabeçalho Authorization.
	options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
	{
		Type = SecuritySchemeType.Http,
		Scheme = "bearer",
		BearerFormat = "JWT",
		Description = "Informe somente o token retornado pelo login (sem a palavra Bearer)."
	});

	options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
	{
		[new OpenApiSecuritySchemeReference("bearer", document)] = []
	});

	// Campo X-Sigera-Sistema nas rotas que consultam os bancos de negócio.
	options.OperationFilter<CabecalhoSistemaOperationFilter>();

	// Exibe os comentários /// dos controllers.
	var arquivoXml = Path.Combine(
		AppContext.BaseDirectory,
		$"{typeof(Program).Assembly.GetName().Name}.xml");

	if (File.Exists(arquivoXml))
	{
		options.IncludeXmlComments(arquivoXml);
	}
});

// Configura a autenticação por tokens JWT Bearer.
builder.Services
.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
	// Define como os tokens recebidos serão validados.
	options.TokenValidationParameters = new TokenValidationParameters
	{
		// Exige que o emissor do token seja o esperado.
		ValidateIssuer = true,
		ValidIssuer = jwtIssuer,


        // Exige que o público-alvo seja o esperado.
        ValidateAudience = true,
		ValidAudience = jwtAudience,

		// Exige uma assinatura válida com a chave configurada.
		ValidateIssuerSigningKey = true,
		IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),

		// Rejeita tokens expirados.
		ValidateLifetime = true,

		// Limita a tolerância de diferença entre relógios.
		ClockSkew = TimeSpan.FromSeconds(30)
	};

	// Depois de validar assinatura e validade, confere se a sessão ainda vale:
	// usuário existe e não está bloqueado, carimbo de segurança atual
	// (muda ao bloquear, trocar grupo, trocar senha) e token sem logout.
	options.Events = new JwtBearerEvents
	{
		OnTokenValidated = async contexto =>
		{
			var principal = contexto.Principal;
			var usuarioId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
			var carimbo = principal?.FindFirstValue(GeradorTokenAcesso.ClaimCarimbo);
			var tokenId = contexto.SecurityToken?.Id;

			contexto.HttpContext.Items[SessaoAtual.ChaveTokenId] = tokenId;
			contexto.HttpContext.Items[SessaoAtual.ChaveExpiracao] =
				contexto.SecurityToken is null
					? null
					: new DateTimeOffset(contexto.SecurityToken.ValidTo, TimeSpan.Zero);

			var sessoes = contexto.HttpContext.RequestServices
				.GetRequiredService<ISessoesService>();

			if (string.IsNullOrWhiteSpace(usuarioId)
				|| !await sessoes.SessaoValidaAsync(
					usuarioId,
					carimbo,
					tokenId,
					contexto.HttpContext.RequestAborted))
			{
				contexto.Fail("Sessão encerrada ou inválida. Faça login novamente.");
			}
		}
	};
});


// Registra os serviços de autorização.
builder.Services.AddAuthorization();

// Limita tentativas de login e envios de e-mail (resposta 429).
builder.Services.AddLimiteRequisicoes();

// Registra os serviços da camada Infrastructure.
// Inclui a conexão com os bancos de negócio e o Identity.
builder.Services.AddSigeraInfrastructure(builder.Configuration);

// Registra o contexto do sistema selecionado.
builder.Services.AddScoped<SigeraSystemContext>();

// Registra o serviço responsável pelo contexto de banco.
builder.Services.AddScoped<SigeraDatabaseService>();

// Registra o serviço de aplicação de associados.
builder.Services.AddScoped<
IAssociadosService,
SigeraAssociados.Application.Services.AssociadosService>();

// Expõe o usuário autenticado para o cálculo dos escopos de acesso.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtual>();

// Registra o serviço de envio de e-mails pelo Gmail.
builder.Services.AddTransient<GmailEmailService>();

// Registra o envio do link de confirmação de e-mail.
// O link usa a URL pública da chave UrlPublica:BaseUrl
// (gravada pelo script scripts/iniciar-tunel-cloudflared.ps1).
builder.Services.AddTransient<EmailConfirmacaoService>();

// Gera os tokens de acesso (JWT) com grupos e carimbo de segurança.
builder.Services.AddScoped<GeradorTokenAcesso>();

// Health checks: /health (API no ar) e /health/pronto (bancos).
builder.Services.AddVerificacoesSaude();

// O cloudflared tunnel recebe a requisição em HTTPS e a repassa
// para a API local em HTTP, informando o esquema original
// em X-Forwarded-Proto. Somente proxies locais (loopback) são aceitos.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
	options.ForwardedHeaders =
		ForwardedHeaders.XForwardedFor |
		ForwardedHeaders.XForwardedProto;
});

// Constrói a aplicação.
var app = builder.Build();

// Executa o provisionamento administrativo somente quando habilitado.
await using (var scope = app.Services.CreateAsyncScope()) 
{ 
	await SeedData.InicializarAsync( 
		scope.ServiceProvider, 
		app.Configuration, 
		app.Environment.IsDevelopment()); 
}

// Trata exceções não capturadas (500 em ProblemDetails, com log).
// Fica no início para cobrir todo o pipeline.
app.UseExceptionHandler();

// Respostas de erro sem corpo (401, 403, 404, 405...) também
// recebem o corpo ProblemDetails.
app.UseStatusCodePages();

// Disponibiliza o Swagger somente em desenvolvimento,
// em /swagger (tela) e /swagger/v1/swagger.json (documento).
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI(options =>
	{
		options.SwaggerEndpoint("/swagger/v1/swagger.json", "SigeraAssociados API v1");
		options.DocumentTitle = "SigeraAssociados API";

		// Mantém o token informado em Authorize ao recarregar a página.
		options.EnablePersistAuthorization();
	});
}

// Aplica X-Forwarded-Proto/For enviados pelo cloudflared.
// Deve vir antes do redirecionamento HTTPS: sem isso, uma requisição
// do túnel chegaria como HTTP e seria redirecionada para a porta local.
app.UseForwardedHeaders();

// Fora de desenvolvimento, orienta o navegador a usar sempre HTTPS (HSTS).
if (!app.Environment.IsDevelopment())
{
	app.UseHsts();
}

// Redireciona requisições HTTP para HTTPS quando configurado.
app.UseHttpsRedirection();

// Autentica a identidade associada ao token Bearer.
// Deve executar antes do middleware de autorização.
app.UseAuthentication();

// Aplica os limites de requisições. Fica depois da autenticação
// para limitar por usuário quando houver token.
app.UseRateLimiter();

// Avalia as regras de autorização dos endpoints.
app.UseAuthorization();

// Seleciona o banco de negócio pelo cabeçalho X-Sigera-Sistema
// e verifica se o usuário tem acesso ao sistema.
// Aplicado somente às rotas de negócio e depois da autorização:
// login, administração e Swagger não exigem o cabeçalho,
// e requisições sem token recebem 401 antes desta verificação.
app.UseWhen(
	contexto => SigeraSystemMiddleware.RotaDeNegocio(contexto.Request.Path),
	ramo => ramo.UseMiddleware<SigeraSystemMiddleware>());

// Mapeia os endpoints definidos nos controllers.
app.MapControllers();

// Health checks (públicos, sem detalhes de erro).
app.MapVerificacoesSaude();

// Inicia a aplicação.
app.Run();
