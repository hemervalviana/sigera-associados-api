using System.Net;
using System.Text.Json;
using Xunit;

namespace SigeraAssociados.IntegrationTests;

/// <summary>
/// Testa os contratos HTTP de validação da API de associados.
/// Os testes usam a autenticação simulada configurada na fábrica.
/// </summary>
public sealed class ConsultaAssociadosValidacaoTests : IClassFixture<SigeraApiFactory>
{
    private readonly HttpClient _client;
    private readonly SigeraApiFactory _factory;

    public ConsultaAssociadosValidacaoTests(SigeraApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Sigera-Sistema", "2");
    }

    /// <summary>
    /// Confirma o status HTTP e o formato JSON de um erro de validação.
    /// </summary>
    private static async Task VerificarErroDeValidacaoAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Contains(
            "application/json",
            response.Content.Headers.ContentType?.MediaType ?? "");

        var conteudo = await response.Content.ReadAsStringAsync();

        using var documento = JsonDocument.Parse(conteudo);

        Assert.Equal(
            JsonValueKind.Object,
            documento.RootElement.ValueKind);
    }

    [Theory]
    [InlineData("pagina=0")]
    [InlineData("pagina=-1")]
    public async Task ConsultarAssociados_ComPaginaInvalida_DeveRetornarBadRequest(
        string parametro)
    {
        using var response = await _client.GetAsync(
            $"/api/associados/consulta?{parametro}");

        await VerificarErroDeValidacaoAsync(response);
    }

    [Theory]
    [InlineData("tamanhoPagina=0")]
    [InlineData("tamanhoPagina=-1")]
    [InlineData("tamanhoPagina=101")]
    public async Task ConsultarAssociados_ComTamanhoPaginaInvalido_DeveRetornarBadRequest(
        string parametro)
    {
        using var response = await _client.GetAsync(
            $"/api/associados/consulta?{parametro}");

        await VerificarErroDeValidacaoAsync(response);
    }

    [Theory]
    [InlineData("pagina=abc")]
    [InlineData("tamanhoPagina=abc")]
    public async Task ConsultarAssociados_ComParametroNumericoInvalido_DeveRetornarBadRequest(
        string parametro)
    {
        using var response = await _client.GetAsync(
            $"/api/associados/consulta?{parametro}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("ordenarPor=campoInexistente")]
    public async Task ConsultarAssociados_ComCampoOrdenacaoInvalido_DeveRetornarBadRequest(
        string parametro)
    {
        using var response = await _client.GetAsync(
            $"/api/associados/consulta?{parametro}");

        await VerificarErroDeValidacaoAsync(response);
    }

    [Theory]
    [InlineData("direcao=aleatoria")]
    [InlineData("direcao=ascending")]
    public async Task ConsultarAssociados_ComDirecaoInvalida_DeveRetornarBadRequest(
        string parametro)
    {
        using var response = await _client.GetAsync(
            $"/api/associados/consulta?{parametro}");

        await VerificarErroDeValidacaoAsync(response);
    }

    [Fact]
    public async Task ConsultarAssociados_SemCabecalhoDoSistema_DeveRetornarBadRequest()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/associados/consulta");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var conteudo = await response.Content.ReadAsStringAsync();

        using var documento = JsonDocument.Parse(conteudo);

        Assert.True(documento.RootElement.TryGetProperty(
            "erro",
            out var erro));

        Assert.Contains(
            "X-Sigera-Sistema",
            erro.GetString() ?? "");
    }

    [Theory]
    [InlineData("5")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task ConsultarAssociados_ComCodigoSistemaForaDoIntervalo_DeveRetornarBadRequest(
        string codigo)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sigera-Sistema", codigo);

        using var response = await client.GetAsync(
            "/api/associados/consulta");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var conteudo = await response.Content.ReadAsStringAsync();

        using var documento = JsonDocument.Parse(conteudo);

        Assert.True(documento.RootElement.TryGetProperty(
            "erro",
            out _));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("2.5")]
    public async Task ConsultarAssociados_ComCodigoSistemaNaoInteiro_DeveRetornarBadRequest(
        string codigo)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sigera-Sistema", codigo);

        using var response = await client.GetAsync(
            "/api/associados/consulta");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConsultarAssociados_ComOrdenacaoInvalida_DeveInformarCamposPermitidos()
    {
        using var response = await _client.GetAsync(
            "/api/associados/consulta?ordenarPor=campoInexistente");

        await VerificarErroDeValidacaoAsync(response);

        var conteudo = await response.Content.ReadAsStringAsync();

        using var documento = JsonDocument.Parse(conteudo);

        Assert.True(documento.RootElement.TryGetProperty(
            "camposPermitidos",
            out var campos));

        var valores = campos
            .EnumerateArray()
            .Select(elemento => elemento.GetString())
            .ToArray();

        Assert.Contains("nome", valores);
        Assert.Contains("cdemp", valores);
        Assert.Contains("sqpescli", valores);
        Assert.Contains("cdsitcli", valores);
        Assert.Contains("situacao", valores);
        Assert.Contains("dtcarteira", valores);
    }

    [Fact]
    public async Task ConsultarAssociados_ComTamanhoPaginaAcimaDoLimite_DeveInformarIntervalo()
    {
        using var response = await _client.GetAsync(
            "/api/associados/consulta?tamanhoPagina=101");

        await VerificarErroDeValidacaoAsync(response);

        var conteudo = await response.Content.ReadAsStringAsync();

        using var documento = JsonDocument.Parse(conteudo);

        Assert.True(documento.RootElement.TryGetProperty(
            "mensagem",
            out var mensagem));

        Assert.Contains(
            "1 e 100",
            mensagem.GetString() ?? "");
    }
}
