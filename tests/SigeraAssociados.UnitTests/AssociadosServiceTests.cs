using SigeraAssociados.Application.Erros;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Interfaces.Acesso;
using SigeraAssociados.Application.Interfaces.Repositories;
using SigeraAssociados.Application.Services;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa o serviço de associados sem acessar o banco de dados.
/// </summary>
public sealed class AssociadosServiceTests
{
    /// <summary>
    /// Repositório falso que registra as chamadas e permite verificar
    /// o comportamento do serviço sem dependências externas.
    /// </summary>
    private sealed class RepositorioFalso : IAssociadosRepository
    {
        public IReadOnlyList<AssociadoDto> Lista { get; set; }
            = Array.Empty<AssociadoDto>();

        public AssociadoDto? AssociadoConsultado { get; set; }

        public ConsultaAssociadosResultado ResultadoPaginado { get; set; }
            = new();

        public ConsultaAssociadosRequest? UltimaConsulta { get; private set; }

        public decimal UltimoCdEmp { get; private set; }

        public decimal UltimoSqPesCli { get; private set; }

        public CancellationToken UltimoCancellationToken { get; private set; }

        public int ChamadasListar { get; private set; }

        public int ChamadasConsultarPorCodigo { get; private set; }

        public int ChamadasConsultaPaginada { get; private set; }

        public EscopoConsulta? UltimoEscopo { get; private set; }

        public Task<IReadOnlyList<AssociadoDto>> ListarAsync(
            EscopoConsulta escopo,
            CancellationToken cancellationToken = default)
        {
            ChamadasListar++;
            UltimoEscopo = escopo;
            UltimoCancellationToken = cancellationToken;

            return Task.FromResult(Lista);
        }

        public Task<AssociadoDto?> ConsultarPorCodigoAsync(
            decimal cdEmp,
            decimal sqPesCli,
            EscopoConsulta escopo,
            CancellationToken cancellationToken = default)
        {
            ChamadasConsultarPorCodigo++;
            UltimoEscopo = escopo;
            UltimoCdEmp = cdEmp;
            UltimoSqPesCli = sqPesCli;
            UltimoCancellationToken = cancellationToken;

            return Task.FromResult(AssociadoConsultado);
        }

        public Task<ConsultaAssociadosResultado> ConsultarPaginadoAsync(
            ConsultaAssociadosRequest request,
            EscopoConsulta escopo,
            CancellationToken cancellationToken = default)
        {
            ChamadasConsultaPaginada++;
            UltimoEscopo = escopo;
            UltimaConsulta = request;
            UltimoCancellationToken = cancellationToken;

            return Task.FromResult(ResultadoPaginado);
        }
    }

    /// <summary>
    /// Escopo de acesso falso, configurável por teste.
    /// </summary>
    private sealed class EscopoFalso : IEscopoAcessoService
    {
        public EscopoConsulta Escopo { get; set; } = EscopoConsulta.Total;

        public Task<bool> PossuiAcessoAoSistemaAsync(
            int sistema,
            CancellationToken cancellationToken = default)
            => Task.FromResult(!Escopo.SemAcesso);

        public Task<EscopoConsulta> ObterEscopoAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(Escopo);
    }

    private static (AssociadosService Servico, RepositorioFalso Repositorio)
        CriarServico(EscopoConsulta? escopo = null)
    {
        var repositorio = new RepositorioFalso();
        var escopoFalso = new EscopoFalso { Escopo = escopo ?? EscopoConsulta.Total };
        var servico = new AssociadosService(repositorio, escopoFalso);

        return (servico, repositorio);
    }

    private static ConsultaAssociadosRequest CriarRequestValido()
    {
        return new ConsultaAssociadosRequest
        {
            Pagina = 1,
            TamanhoPagina = 20,
            OrdenarPor = "nome",
            Direcao = "asc"
        };
    }

    [Fact]
    public async Task ConsultarPaginado_ComPaginaZero_DeveRejeitarSemConsultarRepositorio()
    {
        var (servico, repositorio) = CriarServico();
        var request = new ConsultaAssociadosRequest
        {
            Pagina = 0,
            TamanhoPagina = 20,
            OrdenarPor = "nome",
            Direcao = "asc"
        };

        await Assert.ThrowsAsync<DadosInvalidosException>(
            () => servico.ConsultarPaginadoAsync(request));

        Assert.Equal(0, repositorio.ChamadasConsultaPaginada);
    }

    [Fact]
    public async Task ConsultarPaginado_ComTamanhoPaginaZero_DeveRejeitarSemConsultarRepositorio()
    {
        var (servico, repositorio) = CriarServico();
        var request = new ConsultaAssociadosRequest
        {
            Pagina = 1,
            TamanhoPagina = 0,
            OrdenarPor = "nome",
            Direcao = "asc"
        };

        await Assert.ThrowsAsync<DadosInvalidosException>(
            () => servico.ConsultarPaginadoAsync(request));

        Assert.Equal(0, repositorio.ChamadasConsultaPaginada);
    }

    [Fact]
    public async Task ConsultarPaginado_ComTamanhoPaginaAcimaDoLimite_DeveRejeitar()
    {
        var (servico, repositorio) = CriarServico();
        var request = new ConsultaAssociadosRequest
        {
            Pagina = 1,
            TamanhoPagina = 101,
            OrdenarPor = "nome",
            Direcao = "asc"
        };

        await Assert.ThrowsAsync<DadosInvalidosException>(
            () => servico.ConsultarPaginadoAsync(request));

        Assert.Equal(0, repositorio.ChamadasConsultaPaginada);
    }

    [Fact]
    public async Task ConsultarPaginado_ComCampoOrdenacaoInvalido_DeveRejeitar()
    {
        var (servico, repositorio) = CriarServico();
        var request = new ConsultaAssociadosRequest
        {
            Pagina = 1,
            TamanhoPagina = 20,
            OrdenarPor = "campoInexistente",
            Direcao = "asc"
        };

        await Assert.ThrowsAsync<DadosInvalidosException>(
            () => servico.ConsultarPaginadoAsync(request));

        Assert.Equal(0, repositorio.ChamadasConsultaPaginada);
    }

    [Fact]
    public async Task ConsultarPaginado_ComDirecaoInvalida_DeveRejeitar()
    {
        var (servico, repositorio) = CriarServico();
        var request = new ConsultaAssociadosRequest
        {
            Pagina = 1,
            TamanhoPagina = 20,
            OrdenarPor = "nome",
            Direcao = "aleatoria"
        };

        await Assert.ThrowsAsync<DadosInvalidosException>(
            () => servico.ConsultarPaginadoAsync(request));

        Assert.Equal(0, repositorio.ChamadasConsultaPaginada);
    }

    [Fact]
    public async Task ConsultarPaginado_ComRequestNula_DeveLancarArgumentNullException()
    {
        var (servico, repositorio) = CriarServico();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => servico.ConsultarPaginadoAsync(null!));

        Assert.Equal(0, repositorio.ChamadasConsultaPaginada);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ConsultarPaginado_ComPaginaNegativaOuZero_DeveRejeitar(int pagina)
    {
        var (servico, repositorio) = CriarServico();
        var request = new ConsultaAssociadosRequest
        {
            Pagina = pagina,
            TamanhoPagina = 20
        };

        await Assert.ThrowsAsync<DadosInvalidosException>(
            () => servico.ConsultarPaginadoAsync(request));

        Assert.Equal(0, repositorio.ChamadasConsultaPaginada);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task ConsultarPaginado_ComTamanhoPaginaNosLimites_DeveAceitar(int tamanho)
    {
        var (servico, repositorio) = CriarServico();
        var request = new ConsultaAssociadosRequest
        {
            Pagina = 1,
            TamanhoPagina = tamanho
        };

        await servico.ConsultarPaginadoAsync(request);

        Assert.Equal(1, repositorio.ChamadasConsultaPaginada);
        Assert.Equal(tamanho, repositorio.UltimaConsulta!.TamanhoPagina);
    }

    [Theory]
    [InlineData(" NOME ", "nome")]
    [InlineData("CdEmp", "cdemp")]
    [InlineData("SQPesCli", "sqpescli")]
    [InlineData("CdSitCli", "cdsitcli")]
    [InlineData("Situacao", "situacao")]
    [InlineData("DTCarteira", "dtcarteira")]
    public async Task ConsultarPaginado_ComCampoValido_DeveNormalizarCampo(
        string campoEntrada,
        string campoEsperado)
    {
        var (servico, repositorio) = CriarServico();
        var request = new ConsultaAssociadosRequest
        {
            Pagina = 1,
            TamanhoPagina = 20,
            OrdenarPor = campoEntrada,
            Direcao = "asc"
        };

        await servico.ConsultarPaginadoAsync(request);

        Assert.Equal(campoEsperado, repositorio.UltimaConsulta!.OrdenarPor);
    }

    [Theory]
    [InlineData(" ASC ", "asc")]
    [InlineData("DESC", "desc")]
    public async Task ConsultarPaginado_ComDirecaoValida_DeveNormalizarDirecao(
        string direcaoEntrada,
        string direcaoEsperada)
    {
        var (servico, repositorio) = CriarServico();
        var request = new ConsultaAssociadosRequest
        {
            Pagina = 1,
            TamanhoPagina = 20,
            OrdenarPor = "nome",
            Direcao = direcaoEntrada
        };

        await servico.ConsultarPaginadoAsync(request);

        Assert.Equal(direcaoEsperada, repositorio.UltimaConsulta!.Direcao);
    }

    [Fact]
    public async Task ConsultarPaginado_DevePreservarFiltrosEPaginacao()
    {
        var (servico, repositorio) = CriarServico();
        var request = new ConsultaAssociadosRequest
        {
            CdEmp = 12,
            Nome = "Maria",
            CdSitCli = 3,
            Pagina = 4,
            TamanhoPagina = 50,
            OrdenarPor = "nome",
            Direcao = "desc"
        };

        await servico.ConsultarPaginadoAsync(request);

        var consulta = Assert.IsType<ConsultaAssociadosRequest>(
            repositorio.UltimaConsulta);

        Assert.Equal(12m, consulta.CdEmp);
        Assert.Equal("Maria", consulta.Nome);
        Assert.Equal(3m, consulta.CdSitCli);
        Assert.Equal(4, consulta.Pagina);
        Assert.Equal(50, consulta.TamanhoPagina);
        Assert.Equal("nome", consulta.OrdenarPor);
        Assert.Equal("desc", consulta.Direcao);
    }

    [Fact]
    public async Task ConsultarPaginado_ComValoresPadrao_DeveEncaminharPadroesNormalizados()
    {
        var (servico, repositorio) = CriarServico();
        var request = new ConsultaAssociadosRequest();

        await servico.ConsultarPaginadoAsync(request);

        var consulta = Assert.IsType<ConsultaAssociadosRequest>(
            repositorio.UltimaConsulta);

        Assert.Equal(1, consulta.Pagina);
        Assert.Equal(20, consulta.TamanhoPagina);
        Assert.Equal("nome", consulta.OrdenarPor);
        Assert.Equal("asc", consulta.Direcao);
    }

    [Fact]
    public async Task ConsultarPaginado_DeveRetornarResultadoDoRepositorio()
    {
        var (servico, repositorio) = CriarServico();
        var esperado = new ConsultaAssociadosResultado
        {
            Pagina = 1,
            TamanhoPagina = 20,
            TotalRegistros = 1,
            TotalPaginas = 1,
            Quantidade = 1,
            Associados = new[]
            {
                new AssociadoDto
                {
                    CDEmp = 10,
                    SQPesCli = 25,
                    Nome = "Maria da Silva"
                }
            }
        };

        repositorio.ResultadoPaginado = esperado;

        var resultado = await servico.ConsultarPaginadoAsync(
            CriarRequestValido());

        Assert.Same(esperado, resultado);
        Assert.Equal(1, repositorio.ChamadasConsultaPaginada);
    }

    [Fact]
    public async Task ListarAsync_DeveRetornarListaDoRepositorio()
    {
        var (servico, repositorio) = CriarServico();
        IReadOnlyList<AssociadoDto> esperada = new[]
        {
            new AssociadoDto
            {
                CDEmp = 10,
                SQPesCli = 25,
                Nome = "Maria da Silva"
            }
        };

        repositorio.Lista = esperada;

        var resultado = await servico.ListarAsync();

        Assert.Same(esperada, resultado);
        Assert.Equal(1, repositorio.ChamadasListar);
    }

    [Fact]
    public async Task ConsultarPorCodigoAsync_DeveEncaminharChaveComposta()
    {
        var (servico, repositorio) = CriarServico();
        var esperado = new AssociadoDto
        {
            CDEmp = 10,
            SQPesCli = 25,
            Nome = "Maria da Silva"
        };

        repositorio.AssociadoConsultado = esperado;

        var resultado = await servico.ConsultarPorCodigoAsync(10, 25);

        Assert.Same(esperado, resultado);
        Assert.Equal(1, repositorio.ChamadasConsultarPorCodigo);
        Assert.Equal(10m, repositorio.UltimoCdEmp);
        Assert.Equal(25m, repositorio.UltimoSqPesCli);
    }

    [Fact]
    public async Task ConsultarPorCodigoAsync_QuandoNaoEncontrado_DeveRetornarNulo()
    {
        var (servico, repositorio) = CriarServico();
        repositorio.AssociadoConsultado = null;

        var resultado = await servico.ConsultarPorCodigoAsync(10, 25);

        Assert.Null(resultado);
        Assert.Equal(1, repositorio.ChamadasConsultarPorCodigo);
    }

    [Fact]
    public async Task ConsultarPaginadoAsync_DevePropagarCancellationToken()
    {
        var (servico, repositorio) = CriarServico();
        using var origemCancelamento = new CancellationTokenSource();
        var token = origemCancelamento.Token;

        await servico.ConsultarPaginadoAsync(
            CriarRequestValido(),
            token);

        Assert.Equal(token, repositorio.UltimoCancellationToken);
    }

    [Fact]
    public async Task ListarAsync_DevePropagarCancellationToken()
    {
        var (servico, repositorio) = CriarServico();
        using var origemCancelamento = new CancellationTokenSource();
        var token = origemCancelamento.Token;

        await servico.ListarAsync(token);

        Assert.Equal(token, repositorio.UltimoCancellationToken);
    }

    [Fact]
    public async Task ConsultarPorCodigoAsync_DevePropagarCancellationToken()
    {
        var (servico, repositorio) = CriarServico();
        using var origemCancelamento = new CancellationTokenSource();
        var token = origemCancelamento.Token;

        await servico.ConsultarPorCodigoAsync(10, 25, token);

        Assert.Equal(token, repositorio.UltimoCancellationToken);
    }

    [Fact]
    public async Task ConsultarPaginado_SemAcesso_DeveRetornarVazioSemConsultarRepositorio()
    {
        var (servico, repositorio) = CriarServico(EscopoConsulta.Nenhum);

        var resultado = await servico.ConsultarPaginadoAsync(CriarRequestValido());

        Assert.Equal(0, resultado.TotalRegistros);
        Assert.Empty(resultado.Associados);
        Assert.Equal(0, repositorio.ChamadasConsultaPaginada);
    }

    [Fact]
    public async Task ListarAsync_SemAcesso_DeveRetornarVazioSemConsultarRepositorio()
    {
        var (servico, repositorio) = CriarServico(EscopoConsulta.Nenhum);

        var resultado = await servico.ListarAsync();

        Assert.Empty(resultado);
        Assert.Equal(0, repositorio.ChamadasListar);
    }

    [Fact]
    public async Task ConsultarPorCodigoAsync_SemAcesso_DeveRetornarNuloSemConsultarRepositorio()
    {
        var (servico, repositorio) = CriarServico(EscopoConsulta.Nenhum);

        var resultado = await servico.ConsultarPorCodigoAsync(10, 25);

        Assert.Null(resultado);
        Assert.Equal(0, repositorio.ChamadasConsultarPorCodigo);
    }

    [Fact]
    public async Task ConsultarPaginado_DeveEncaminharEscopoDoUsuario()
    {
        var escopo = EscopoConsulta.Criar(new[] { new EscopoAcesso(1, 3, null) });
        var (servico, repositorio) = CriarServico(escopo);

        await servico.ConsultarPaginadoAsync(CriarRequestValido());

        Assert.Same(escopo, repositorio.UltimoEscopo);
    }

    [Theory]
    [InlineData("123.456.789-00")]
    [InlineData("1234")]
    [InlineData("abc")]
    public async Task ConsultarPaginado_ComCpfInvalido_DeveRejeitarSemConsultarRepositorio(string cpf)
    {
        var (servico, repositorio) = CriarServico();
        var request = new ConsultaAssociadosRequest
        {
            Cpf = cpf,
            Pagina = 1,
            TamanhoPagina = 20,
            OrdenarPor = "nome",
            Direcao = "asc"
        };

        await Assert.ThrowsAsync<DadosInvalidosException>(
            () => servico.ConsultarPaginadoAsync(request));

        Assert.Equal(0, repositorio.ChamadasConsultaPaginada);
    }
}
