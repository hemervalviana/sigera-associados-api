using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SigeraAssociados.Api.Services;
using Xunit;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Testa a montagem do link de confirmação e a codificação do token.
/// </summary>
public sealed class EmailConfirmacaoServiceTests
{
	// Token com os caracteres que quebrariam uma URL sem codificação.
	private const string TokenIdentity = "CfDJ8Hgx+abc/def==";

	private static EmailConfirmacaoService Criar(string? urlPublica)
	{
		var configuracao = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				[EmailConfirmacaoService.ChaveUrlPublica] = urlPublica
			})
			.Build();

		// CriarLink usa apenas a configuração.
		return new EmailConfirmacaoService(
			null!,
			null!,
			configuracao,
			NullLogger<EmailConfirmacaoService>.Instance);
	}

	[Theory]
	[InlineData("https://abc.trycloudflare.com")]
	[InlineData("https://abc.trycloudflare.com/")]
	public void CriarLink_ComUrlHttps_DeveMontarOLinkDeConfirmacao(string urlPublica)
	{
		var link = Criar(urlPublica).CriarLink("usuario-1", TokenIdentity);

		Assert.NotNull(link);
		Assert.StartsWith("https://abc.trycloudflare.com/api/auth/confirmar-email?", link);

		var parametros = QueryHelpers.ParseQuery(new Uri(link!).Query);

		Assert.Equal("usuario-1", parametros["usuarioId"].ToString());
		Assert.Equal(
			TokenIdentity,
			EmailConfirmacaoService.DecodificarToken(parametros["token"].ToString()));
	}

	[Fact]
	public void CriarLink_ComLocalhostHttp_DeveSerAceito()
	{
		Assert.NotNull(Criar("http://localhost:5092").CriarLink("u", TokenIdentity));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("nao-e-url")]
	[InlineData("http://abc.trycloudflare.com")]
	[InlineData("https://abc.trycloudflare.com/?x=1")]
	public void CriarLink_SemUrlValida_DeveDevolverNull(string? urlPublica)
	{
		Assert.Null(Criar(urlPublica).CriarLink("u", TokenIdentity));
	}

	[Fact]
	public void DecodificarToken_ComValorInvalido_DeveDevolverNull()
	{
		Assert.Null(EmailConfirmacaoService.DecodificarToken("!!!"));
	}
}
