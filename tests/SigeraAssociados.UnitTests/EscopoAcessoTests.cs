using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Repositories;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa a regra de escopo (empresa, filial e carteira do corretor)
/// aplicada pelo AssociadosRepository, usando o provedor InMemory.
/// O provedor InMemory valida a lógica, não a tradução para SQL:
/// confirme também com uma consulta real em um banco de teste.
/// </summary>
public sealed class EscopoAcessoTests
{
	// Massa de dados:
	// Empresa 1: associados 101 (filial 3), 102 (filial 5), 103 (sem filial)
	// Empresa 2: associado 201 (filial 3)
	// Contratos: corretor 900 -> associados 101 e 102 (empresa 1)
	//            corretor 900 -> associado 201 (empresa 2)
	//            corretor 901 -> associado 103 (empresa 1)
	private static SigeraDbContext CriarContexto()
	{
		var opcoes = new DbContextOptionsBuilder<SigeraDbContext>()
			.UseInMemoryDatabase($"escopo-{Guid.NewGuid()}")
			.Options;

		var db = new SigeraDbContext(opcoes);

		db.Associados.AddRange(
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 101, CD_FIL = 3 },
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 102, CD_FIL = 5 },
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 103, CD_FIL = null },
			new T000CLIE { CD_EMP = 2, SQ_PES_CLI = 201, CD_FIL = 3 });

		db.Contratos.AddRange(
			new T009CTRT { CD_EMP = 1, CD_FIL = 3, SQ_CTR = 1, SQ_PES_CLI = 101, SQ_PES_CRO = 900 },
			new T009CTRT { CD_EMP = 1, CD_FIL = 5, SQ_CTR = 2, SQ_PES_CLI = 102, SQ_PES_CRO = 900 },
			new T009CTRT { CD_EMP = 2, CD_FIL = 3, SQ_CTR = 1, SQ_PES_CLI = 201, SQ_PES_CRO = 900 },
			new T009CTRT { CD_EMP = 1, CD_FIL = 1, SQ_CTR = 3, SQ_PES_CLI = 103, SQ_PES_CRO = 901 });

		db.SaveChanges();

		return db;
	}

	private static string[] Visiveis(EscopoConsulta escopo)
	{
		using var db = CriarContexto();

		return AssociadosRepository
			.AplicarEscopo(db.Associados, db.Contratos, escopo)
			.OrderBy(a => a.CD_EMP)
			.ThenBy(a => a.SQ_PES_CLI)
			.AsEnumerable()
			.Select(a => $"{a.CD_EMP}-{a.SQ_PES_CLI}")
			.ToArray();
	}

	private static EscopoConsulta Escopo(params EscopoAcesso[] escopos) =>
		EscopoConsulta.Criar(escopos);

	[Fact]
	public void AcessoTotal_DeveVerTodos()
	{
		Assert.Equal(
			new[] { "1-101", "1-102", "1-103", "2-201" },
			Visiveis(EscopoConsulta.Total));
	}

	[Fact]
	public void SemAcesso_NaoDeveVerNenhum()
	{
		Assert.Empty(Visiveis(EscopoConsulta.Nenhum));
	}

	[Fact]
	public void SistemaInteiro_EquivaleAAcessoTotal()
	{
		var escopo = Escopo(new EscopoAcesso(null, null, null));

		Assert.True(escopo.AcessoTotal);
	}

	[Fact]
	public void EmpresaInteira_DeveVerTodasAsFiliaisESemFilial()
	{
		Assert.Equal(
			new[] { "1-101", "1-102", "1-103" },
			Visiveis(Escopo(new EscopoAcesso(1, null, null))));
	}

	[Fact]
	public void EmpresaEFilial_DeveVerSomenteAFilial_SemAssociadosSemFilial()
	{
		Assert.Equal(
			new[] { "1-101" },
			Visiveis(Escopo(new EscopoAcesso(1, 3, null))));
	}

	[Fact]
	public void DuasFiliais_DevemSeSomar()
	{
		Assert.Equal(
			new[] { "1-101", "1-102" },
			Visiveis(Escopo(
				new EscopoAcesso(1, 3, null),
				new EscopoAcesso(1, 5, null))));
	}

	[Fact]
	public void FilialDeUmaEmpresa_NaoDeveVazarParaOutraEmpresa()
	{
		// Filial 3 existe nas empresas 1 e 2; o escopo é só da empresa 1.
		Assert.DoesNotContain(
			"2-201",
			Visiveis(Escopo(new EscopoAcesso(1, 3, null))));
	}

	[Fact]
	public void CarteiraDoCorretor_DeveVerSomenteSeusAssociadosNaEmpresa()
	{
		// O corretor 900 também tem contrato na empresa 2,
		// mas o escopo é só da empresa 1.
		Assert.Equal(
			new[] { "1-101", "1-102" },
			Visiveis(Escopo(new EscopoAcesso(1, null, 900))));
	}

	[Fact]
	public void CarteiraDoCorretorNaFilial_DeveRestringirPelaFilialDoAssociado()
	{
		Assert.Equal(
			new[] { "1-101" },
			Visiveis(Escopo(new EscopoAcesso(1, 3, 900))));
	}

	[Fact]
	public void CarteiraDoCorretor_NaoDeveVerAssociadosDeOutroCorretor()
	{
		Assert.DoesNotContain(
			"1-103",
			Visiveis(Escopo(new EscopoAcesso(1, null, 900))));
	}

	[Fact]
	public void CarteiraDoCorretorNaFilial_NaoDeveVerAssociadoSemFilial()
	{
		// Associado 103 não tem filial: só aparece para quem tem a empresa inteira.
		Assert.Empty(Visiveis(Escopo(new EscopoAcesso(1, 3, 901))));
	}

	[Fact]
	public void EscoposDiferentes_DevemSeSomar()
	{
		Assert.Equal(
			new[] { "1-101", "2-201" },
			Visiveis(Escopo(
				new EscopoAcesso(1, 3, null),
				new EscopoAcesso(2, null, null))));
	}
}
