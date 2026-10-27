using SigeraAssociados.Application.Orgaos;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras de formato de órgão, lotação e vínculo.
/// </summary>
public sealed class ValidacaoOrgaosTests
{
	[Fact]
	public void OrgaoMinimo_DeveSerValido()
	{
		Assert.Null(ValidacaoOrgaos.Orgao(new OrgaoDados { Nome = "SEDUC" }, inclusao: true));
	}

	[Fact]
	public void OrgaoCompleto_DeveSerValido()
	{
		var orgao = new OrgaoDados
		{
			CdOrg = 120,
			Nome = "SECRETARIA DA EDUCACAO",
			Sigla = "SEDUC",
			Uf = "CE",
			CdFil = 1,
			CdSituacao = 1,
			Endereco = new EnderecoDados { Logradouro = "AV. A", Numero = 10, Cep = "60822-325", CdMunicipio = 2304400 },
			ContatoAdministrativo = new ContatoDados { Nome = "MARIA", Telefone = "(85) 3101-0000", Email = "adm@seduc.ce.gov.br" },
			EmpresaProcessadora = new EmpresaProcessadoraDados { Nome = "ETICE", Telefone = "85 31010002" }
		};

		Assert.Null(ValidacaoOrgaos.Orgao(orgao, inclusao: true));
	}

	[Fact]
	public void OrgaoSemNome_DeveSerRecusado()
	{
		Assert.Contains("nome", ValidacaoOrgaos.Orgao(new OrgaoDados(), inclusao: true));
	}

	[Fact]
	public void NomeComMaisDe50_DeveSerRecusado()
	{
		Assert.Contains("50", ValidacaoOrgaos.Orgao(new OrgaoDados { Nome = new string('A', 51) }, inclusao: true));
	}

	[Theory]
	[InlineData(2)]
	[InlineData(9)]
	[InlineData(10)]
	public void SituacaoDoOrgaoDiferenteDe0e1_DeveSerRecusada(int situacao)
	{
		Assert.Contains("ATIVO", ValidacaoOrgaos.Orgao(new OrgaoDados { Nome = "X", CdSituacao = situacao }, inclusao: true));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public void SituacaoDoOrgao0ou1_DeveSerAceita(int situacao)
	{
		Assert.Null(ValidacaoOrgaos.Orgao(new OrgaoDados { Nome = "X", CdSituacao = situacao }, inclusao: true));
	}

	[Fact]
	public void SituacaoDoOrgao_DeveTerDescricaoFixa()
	{
		Assert.Equal("ATIVO", SituacaoOrgao.Descricao(1));
		Assert.Equal("INATIVO", SituacaoOrgao.Descricao(0));
		Assert.Null(SituacaoOrgao.Descricao(null));
		Assert.Null(SituacaoOrgao.Descricao(5));
	}

	[Theory]
	[InlineData("123")]
	[InlineData("1234567890123456")]
	[InlineData("85 ABCD-0000")]
	public void TelefoneInvalido_DeveSerRecusado(string telefone)
	{
		var orgao = new OrgaoDados { Nome = "X", ContatoFinanceiro = new ContatoDados { Telefone = telefone } };

		Assert.Contains("contatoFinanceiro.telefone", ValidacaoOrgaos.Orgao(orgao, inclusao: true));
	}

	[Fact]
	public void CepDaEmpresaProcessadoraInvalido_DeveSerRecusado()
	{
		var orgao = new OrgaoDados
		{
			Nome = "X",
			EmpresaProcessadora = new EmpresaProcessadoraDados { Endereco = new EnderecoDados { Cep = "123" } }
		};

		Assert.Contains("empresaProcessadora.endereco.cep", ValidacaoOrgaos.Orgao(orgao, inclusao: true));
	}

	[Fact]
	public void Lotacao_SemDescricao_DeveSerRecusada()
	{
		Assert.Contains("descricao", ValidacaoOrgaos.Lotacao(new LotacaoDados(), inclusao: true));
	}

	[Fact]
	public void Lotacao_Valida()
	{
		Assert.Null(ValidacaoOrgaos.Lotacao(new LotacaoDados { Descricao = "ESCOLA", CdSituacao = 1 }, inclusao: true));
		Assert.Null(ValidacaoOrgaos.Lotacao(new LotacaoDados { Descricao = "ESCOLA", CdSituacao = 2 }, inclusao: true));
		Assert.Null(ValidacaoOrgaos.Lotacao(new LotacaoDados { Descricao = "ESCOLA" }, inclusao: true));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(3)]
	public void Lotacao_SituacaoDiferenteDe1e2_DeveSerRecusada(int situacao)
	{
		Assert.Contains("2 (INATIVO)",
			ValidacaoOrgaos.Lotacao(new LotacaoDados { Descricao = "ESCOLA", CdSituacao = situacao }, inclusao: true));
	}

	[Theory]
	[InlineData(2)]
	[InlineData(9)]
	public void Cargo_SituacaoDiferenteDe0e1_DeveSerRecusada(int situacao)
	{
		Assert.Contains("0 (INATIVO)",
			ValidacaoOrgaos.Cargo(new CargoDados { Descricao = "PROFESSOR", CdSituacao = situacao }, inclusao: true));
	}

	[Fact]
	public void Situacoes_DescricoesPorTabela()
	{
		Assert.Equal("INATIVO", SituacaoAtivoInativo.Lotacao.Descricao(2));
		Assert.Null(SituacaoAtivoInativo.Lotacao.Descricao(0));
		Assert.Equal("INATIVO", SituacaoAtivoInativo.Cargo.Descricao(0));
		Assert.Equal("ATIVO", SituacaoAtivoInativo.Cargo.Descricao(1));
		Assert.Equal("INATIVO", SituacaoAtivoInativo.Orgao.Descricao(0));
	}

	[Fact]
	public void Grupo_Valido()
	{
		Assert.Null(ValidacaoOrgaos.Grupo(
			new GrupoDados { Descricao = "ESTADUAL", Tipo = "F", Ordem = "A", NumeroDias = 1 }, inclusao: true));
		Assert.Null(ValidacaoOrgaos.Grupo(
			new GrupoDados { Descricao = "MUNICIPAL", Tipo = "u", Ordem = "d", NumeroDias = 31 }, inclusao: true));
	}

	[Fact]
	public void Grupo_DescricoesFixas()
	{
		Assert.Equal("DIA FIXO", RegrasGrupoOrgao.DescricaoTipo("F"));
		Assert.Equal("DIA ÚTIL", RegrasGrupoOrgao.DescricaoTipo("u"));
		Assert.Equal("ASCENDENTE", RegrasGrupoOrgao.DescricaoOrdem("A"));
		Assert.Equal("DESCENDENTE", RegrasGrupoOrgao.DescricaoOrdem("D"));
		Assert.Null(RegrasGrupoOrgao.DescricaoTipo("X"));
	}

	[Fact]
	public void Grupo_SemCamposObrigatorios_DeveSerRecusado()
	{
		Assert.Contains("numeroDias", ValidacaoOrgaos.Grupo(new GrupoDados { Descricao = "X", Tipo = "F", Ordem = "A" }, inclusao: true));
		Assert.Contains("tipo", ValidacaoOrgaos.Grupo(new GrupoDados { Descricao = "X", Ordem = "A", NumeroDias = 1 }, inclusao: true));
		Assert.Contains("ordem", ValidacaoOrgaos.Grupo(new GrupoDados { Descricao = "X", Tipo = "F", NumeroDias = 1 }, inclusao: true));
	}

	[Theory]
	[InlineData("E", "A", 1)]
	[InlineData("FF", "A", 1)]
	[InlineData("F", "N", 1)]
	[InlineData("F", "A", 0)]
	[InlineData("F", "A", 32)]
	public void Grupo_ForaDosValoresPermitidos_DeveSerRecusado(string tipo, string ordem, int numeroDias)
	{
		Assert.NotNull(ValidacaoOrgaos.Grupo(
			new GrupoDados { Descricao = "X", Tipo = tipo, Ordem = ordem, NumeroDias = numeroDias }, inclusao: true));
	}

	[Fact]
	public void SubOrgao_SemNome_DeveSerRecusado()
	{
		Assert.Contains("nome", ValidacaoOrgaos.SubOrgao(new SubOrgaoDados()));
		Assert.Null(ValidacaoOrgaos.SubOrgao(new SubOrgaoDados { Nome = "REGIONAL" }));
	}

	[Fact]
	public void Cargo_Valido()
	{
		Assert.Null(ValidacaoOrgaos.Cargo(new CargoDados { Descricao = "PROFESSOR", CdSituacao = 1 }, inclusao: true));
	}

	[Fact]
	public void Cargo_DescricaoLonga_DeveSerRecusada()
	{
		Assert.Contains("30", ValidacaoOrgaos.Cargo(new CargoDados { Descricao = new string('A', 31) }, inclusao: true));
	}

	[Fact]
	public void Cargo_CodigoAcimaDe9999_DeveSerRecusado()
	{
		Assert.Contains("cdCargo", ValidacaoOrgaos.Cargo(new CargoDados { CdCargo = 10_000, Descricao = "X" }, inclusao: true));
	}

	[Fact]
	public void Vinculo_SemOrgao_DeveSerRecusado()
	{
		Assert.Contains("cdOrg", ValidacaoOrgaos.Vinculo(new VinculoOrgaoDados()));
	}

	[Fact]
	public void Vinculo_Valido()
	{
		var vinculo = new VinculoOrgaoDados
		{
			CdOrg = 120,
			CdLotacao = 35,
			CdSituacao = 1,
			Matricula = "123456",
			DigitoMatricula = "7",
			Salario = 3500.00m,
			SalarioLiquido = 2980.50m
		};

		Assert.Null(ValidacaoOrgaos.Vinculo(vinculo));
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(10000000)]
	[InlineData(10.555)]
	public void Vinculo_SalarioInvalido_DeveSerRecusado(double salario)
	{
		Assert.Contains("salario", ValidacaoOrgaos.Vinculo(new VinculoOrgaoDados { CdOrg = 1, Salario = (decimal)salario }));
	}

	[Fact]
	public void Vinculo_MatriculaLonga_DeveSerRecusada()
	{
		Assert.Contains("matricula", ValidacaoOrgaos.Vinculo(new VinculoOrgaoDados { CdOrg = 1, Matricula = new string('1', 21) }));
	}
}
