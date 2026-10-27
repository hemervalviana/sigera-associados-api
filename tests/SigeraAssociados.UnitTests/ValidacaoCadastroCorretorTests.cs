using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Corretores;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras do cadastro de corretor: física ou jurídica (nunca as
/// duas, sempre uma), documentos, comissões e senha do representante.
/// </summary>
public sealed class ValidacaoCadastroCorretorTests
{
	private static CadastroCorretorDados Fisica(
		string? cpf = "529.982.247-25",
		DadosCorretor? corretor = null,
		PessoaJuridicaDados? juridica = null,
		string tipo = "F") => new()
	{
		TipoPessoa = tipo,
		Nome = "JOAO CORRETOR",
		PessoaFisica = new PessoaFisicaDados { Cpf = cpf },
		PessoaJuridica = juridica,
		Corretor = corretor
	};

	private static CadastroCorretorDados Juridica(string? cnpj = "11.222.333/0001-81", PessoaFisicaDados? fisica = null) => new()
	{
		TipoPessoa = "J",
		Nome = "CORRETORA EXEMPLO LTDA",
		PessoaJuridica = new PessoaJuridicaDados { Cnpj = cnpj },
		PessoaFisica = fisica
	};

	// ----- Física ou jurídica -----

	[Fact]
	public void PessoaFisicaSoComCpf_DeveSerValida()
	{
		Assert.Null(ValidacaoCadastroCorretor.Validar(Fisica()));
	}

	[Fact]
	public void PessoaJuridicaSoComCnpj_DeveSerValida()
	{
		Assert.Null(ValidacaoCadastroCorretor.Validar(Juridica()));
	}

	[Theory]
	[InlineData("f")]
	[InlineData(" F ")]
	public void TipoEmMinusculaOuComEspaco_DeveSerAceito(string tipo)
	{
		Assert.Null(ValidacaoCadastroCorretor.Validar(Fisica(tipo: tipo)));
	}

	[Theory]
	[InlineData("")]
	[InlineData("X")]
	[InlineData("FJ")]
	public void TipoInvalido_DeveSerRecusado(string tipo)
	{
		Assert.Contains("tipoPessoa", ValidacaoCadastroCorretor.Validar(Fisica(tipo: tipo)));
	}

	[Fact]
	public void FisicaComBlocoDeJuridica_DeveSerRecusada()
	{
		Assert.Contains("só pessoaFisica",
			ValidacaoCadastroCorretor.Validar(Fisica(juridica: new PessoaJuridicaDados { Cnpj = "11.222.333/0001-81" })));
	}

	[Fact]
	public void JuridicaComBlocoDeFisica_DeveSerRecusada()
	{
		Assert.Contains("só pessoaJuridica",
			ValidacaoCadastroCorretor.Validar(Juridica(fisica: new PessoaFisicaDados { Cpf = "529.982.247-25" })));
	}

	[Fact]
	public void FisicaSemBlocoDeFisica_DeveSerRecusada()
	{
		var dados = new CadastroCorretorDados { TipoPessoa = "F", Nome = "X" };

		Assert.Contains("pessoaFisica", ValidacaoCadastroCorretor.Validar(dados));
	}

	[Fact]
	public void JuridicaSemBlocoDeJuridica_DeveSerRecusada()
	{
		var dados = new CadastroCorretorDados { TipoPessoa = "J", Nome = "X" };

		Assert.Contains("pessoaJuridica", ValidacaoCadastroCorretor.Validar(dados));
	}

	// ----- Documentos -----

	[Theory]
	[InlineData(null)]
	[InlineData("123.456.789-00")]
	[InlineData("111.111.111-11")]
	public void CpfAusenteOuInvalido_DeveSerRecusado(string? cpf)
	{
		Assert.Contains("CPF", ValidacaoCadastroCorretor.Validar(Fisica(cpf: cpf)));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("11.222.333/0001-00")]
	[InlineData("11.111.111/1111-11")]
	public void CnpjAusenteOuInvalido_DeveSerRecusado(string? cnpj)
	{
		Assert.Contains("CNPJ", ValidacaoCadastroCorretor.Validar(Juridica(cnpj: cnpj)));
	}

	[Fact]
	public void SemNome_DeveSerRecusado()
	{
		var dados = new CadastroCorretorDados { TipoPessoa = "F", PessoaFisica = new PessoaFisicaDados { Cpf = "529.982.247-25" } };

		Assert.Contains("nome", ValidacaoCadastroCorretor.Validar(dados));
	}

	[Fact]
	public void JuridicaComCargo_DeveSerValida()
	{
		var dados = new CadastroCorretorDados
		{
			TipoPessoa = "J",
			Nome = "CORRETORA EXEMPLO LTDA",
			PessoaJuridica = new PessoaJuridicaDados { Cnpj = "11.222.333/0001-81", Contato = "MARIA", CdCargo = 1 }
		};

		Assert.Null(ValidacaoCadastroCorretor.Validar(dados));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(10_000)]
	public void CargoForaDaFaixa_DeveSerRecusado(int cargo)
	{
		var dados = new CadastroCorretorDados
		{
			TipoPessoa = "J",
			Nome = "CORRETORA EXEMPLO LTDA",
			PessoaJuridica = new PessoaJuridicaDados { Cnpj = "11.222.333/0001-81", CdCargo = cargo }
		};

		Assert.Contains("cdCargo", ValidacaoCadastroCorretor.Validar(dados));
	}

	// ----- Dados do corretor -----

	[Fact]
	public void CorretorCompleto_DeveSerValido()
	{
		Assert.Null(ValidacaoCadastroCorretor.Validar(Fisica(corretor: new DadosCorretor
		{
			CdFil = 1,
			CdMunicipio = 2307304,
			CdSituacao = 1,
			Classe = "A",
			Tipo = 2,
			InscricaoSusep = 123_456_789_012_345m,
			ComissaoAdesao = 10,
			ComissaoCarencia = 5.5m,
			ComissaoPlano = 100,
			SqPesRepresentante = 10,
			SenhaRepresentante = "Senha@2026",
			SqCoordenador = 1,
			SqAgente = 1,
			SqGerenteVendas = 1
		})));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(100.01)]
	[InlineData(10.555)]
	public void ComissaoForaDaFaixa_DeveSerRecusada(double comissao)
	{
		Assert.Contains("comissaoAdesao",
			ValidacaoCadastroCorretor.Validar(Fisica(corretor: new DadosCorretor { ComissaoAdesao = (decimal)comissao })));
	}

	[Fact]
	public void ClasseComMaisDeUmCaractere_DeveSerRecusada()
	{
		Assert.Contains("classe", ValidacaoCadastroCorretor.Validar(Fisica(corretor: new DadosCorretor { Classe = "AB" })));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	public void SituacaoAtivoOuInativo_DeveSerValida(int situacao)
	{
		Assert.Null(ValidacaoCadastroCorretor.Validar(Fisica(corretor: new DadosCorretor { CdSituacao = situacao })));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(3)]
	[InlineData(10)]
	public void SituacaoForaDaLista_DeveSerRecusada(int situacao)
	{
		Assert.Contains("cdSituacao", ValidacaoCadastroCorretor.Validar(Fisica(corretor: new DadosCorretor { CdSituacao = situacao })));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(3)]
	public void TipoForaDaLista_DeveSerRecusado(int tipo)
	{
		Assert.Contains("tipo", ValidacaoCadastroCorretor.Validar(Fisica(corretor: new DadosCorretor { Tipo = tipo })));
	}

	[Fact]
	public void RepresentanteSemFilial_DeveSerRecusado()
	{
		Assert.Contains("cdFil", ValidacaoCadastroCorretor.Validar(Fisica(corretor: new DadosCorretor { SqPesRepresentante = 500 })));
	}

	[Fact]
	public void TipoESituacao_DevemTrazerADescricaoDoLegado()
	{
		Assert.Equal("CORRETOR", TipoCorretor.Descricao(1));
		Assert.Equal("COLABORADOR", TipoCorretor.Descricao(2));
		Assert.Null(TipoCorretor.Descricao(3));
		Assert.Equal("ATIVO", SigeraAssociados.Application.Orgaos.SituacaoAtivoInativo.Corretor.Descricao(1));
		Assert.Equal("INATIVO", SigeraAssociados.Application.Orgaos.SituacaoAtivoInativo.Corretor.Descricao(2));
	}

	// ----- Agentes e gerentes de vendas -----

	[Fact]
	public void AgenteCompleto_DeveSerValido()
	{
		Assert.Null(ValidacaoApoioCorretor.Agente(new AgenteDados
		{
			Descricao = "AGENTE CENTRO",
			CdBanco = 104,
			DigitoBanco = "0",
			Agencia = 1234,
			DigitoAgencia = "5",
			Conta = 98765,
			DigitoConta = "1",
			Operacao = 13
		}, inclusao: true));
	}

	[Fact]
	public void AgenteSemNome_DeveSerRecusado()
	{
		Assert.Contains("descricao", ValidacaoApoioCorretor.Agente(new AgenteDados { CdBanco = 104 }, inclusao: true));
	}

	[Fact]
	public void AgenteComDigitoDaContaLongo_DeveSerRecusado()
	{
		Assert.Contains("digitoConta", ValidacaoApoioCorretor.Agente(new AgenteDados { Descricao = "X", DigitoConta = "123" }, inclusao: false));
	}

	[Fact]
	public void GerenteVendasCompleto_DeveSerValido()
	{
		Assert.Null(ValidacaoApoioCorretor.GerenteVendas(new GerenteVendasDados { Descricao = "JOSE", PercentualComissao = 2.5m }, inclusao: true));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(100.5)]
	[InlineData(1.234)]
	public void GerenteVendasComPercentualInvalido_DeveSerRecusado(double percentual)
	{
		Assert.Contains("percentualComissao",
			ValidacaoApoioCorretor.GerenteVendas(new GerenteVendasDados { Descricao = "JOSE", PercentualComissao = (decimal)percentual }, inclusao: false));
	}

	// ----- Senha do representante -----

	[Theory]
	[InlineData("")]
	[InlineData("1234")]
	[InlineData(" com espaço ")]
	public void SenhaAceita_DeveSerValida(string senha)
	{
		Assert.Null(ValidacaoCadastroCorretor.Validar(Fisica(corretor: new DadosCorretor { SenhaRepresentante = senha })));
	}

	[Fact]
	public void SenhaComMaisDe20_DeveSerRecusada()
	{
		Assert.Contains("senhaRepresentante",
			ValidacaoCadastroCorretor.Validar(Fisica(corretor: new DadosCorretor { SenhaRepresentante = new string('x', 21) })));
	}

	[Theory]
	[InlineData("tab\taqui")]
	[InlineData("emoji😀")]
	public void SenhaComCaractereQueOVarcharNaoGuarda_DeveSerRecusada(string senha)
	{
		Assert.Contains("senhaRepresentante",
			ValidacaoCadastroCorretor.Validar(Fisica(corretor: new DadosCorretor { SenhaRepresentante = senha })));
	}

	// ----- CNPJ -----

	[Fact]
	public void Cnpj_DeveSerFormatadoComZerosAEsquerda()
	{
		Assert.Equal("11.222.333/0001-81", DocumentoCnpj.Formatar(11_222_333_000_181m));
		Assert.Equal("01.222.333/0001-90", DocumentoCnpj.Formatar(1_222_333_000_190m));
		Assert.Null(DocumentoCnpj.Formatar(null));
	}

	[Theory]
	[InlineData("11222333000181", true)]
	[InlineData("11.222.333/0001-81", false)] // Valido recebe só os dígitos
	[InlineData("11222333000180", false)]
	public void Cnpj_DigitosVerificadores(string digitos, bool valido)
	{
		Assert.Equal(valido, DocumentoCnpj.Valido(digitos));
	}

	// ----- Parametrização (classificação, status, agente e gerente) -----

	[Fact]
	public void ParametrizacaoCompleta_DeveSerValida()
	{
		Assert.Null(ValidacaoCadastroCorretor.Parametrizacao(new ParametrizacaoCorretorDados
		{
			Tipo = 2, CdSituacao = 1, SqAgente = 15, SqGerenteVendas = 3
		}));
	}

	[Fact]
	public void ParametrizacaoSemAgenteNemGerente_DeveSerValida()
	{
		Assert.Null(ValidacaoCadastroCorretor.Parametrizacao(new ParametrizacaoCorretorDados { Tipo = 1, CdSituacao = 2 }));
	}

	[Fact]
	public void ParametrizacaoNula_DeveSerRecusada()
	{
		Assert.NotNull(ValidacaoCadastroCorretor.Parametrizacao(null));
	}

	[Theory]
	[InlineData(null, 1, "tipo")]
	[InlineData(3, 1, "tipo")]
	[InlineData(0, 1, "tipo")]
	[InlineData(1, null, "cdSituacao")]
	[InlineData(1, 3, "cdSituacao")]
	public void ParametrizacaoComTipoOuStatusInvalido_DeveSerRecusada(int? tipo, int? situacao, string campo)
	{
		var erro = ValidacaoCadastroCorretor.Parametrizacao(new ParametrizacaoCorretorDados
		{
			Tipo = tipo, CdSituacao = situacao
		});

		Assert.NotNull(erro);
		Assert.Contains(campo, erro);
	}

	[Theory]
	[InlineData(0.0, null, "sqAgente")]
	[InlineData(1.5, null, "sqAgente")]
	[InlineData(null, -1.0, "sqGerenteVendas")]
	public void ParametrizacaoComAgenteOuGerenteInvalido_DeveSerRecusada(double? agente, double? gerente, string campo)
	{
		var erro = ValidacaoCadastroCorretor.Parametrizacao(new ParametrizacaoCorretorDados
		{
			Tipo = 1, CdSituacao = 1,
			SqAgente = (decimal?)agente, SqGerenteVendas = (decimal?)gerente
		});

		Assert.NotNull(erro);
		Assert.Contains(campo, erro);
	}
}
