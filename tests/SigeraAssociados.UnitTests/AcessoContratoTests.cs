using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Acesso;
using SigeraAssociados.Infrastructure.Contratos;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Quais contratos o usuário alcança (provedor InMemory: valida a lógica,
/// não a tradução para SQL) e o acerto de QT_PCL/DT_ULT_DCT.
/// </summary>
public sealed class AcessoContratoTests
{
	// Empresa 1. Associados 101, 102 e 103 (103 excluído pela API).
	// Contratos (filial/sq -> associado, corretor):
	//   1/1 -> 101, 900     1/2 -> 102, 901     2/1 -> 101, 900
	//   2/2 -> 102, 900 (excluído pela API)     1/3 -> 103, 900 (associado excluído)
	private static SigeraDbContext CriarContexto()
	{
		var db = new SigeraDbContext(new DbContextOptionsBuilder<SigeraDbContext>()
			.UseInMemoryDatabase($"contratos-{Guid.NewGuid()}")
			.Options);

		db.Associados.AddRange(
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 101, CD_FIL = 1 },
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 102, CD_FIL = 1 },
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 103, CD_FIL = 1, DT_EXC_API = DateTime.Today });

		db.Contratos.AddRange(
			new T009CTRT { CD_EMP = 1, CD_FIL = 1, SQ_CTR = 1, SQ_PES_CLI = 101, SQ_PES_CRO = 900, CD_PRD = 2 },
			new T009CTRT { CD_EMP = 1, CD_FIL = 1, SQ_CTR = 2, SQ_PES_CLI = 102, SQ_PES_CRO = 901, CD_PRD = 1 },
			new T009CTRT { CD_EMP = 1, CD_FIL = 2, SQ_CTR = 1, SQ_PES_CLI = 101, SQ_PES_CRO = 900 },
			new T009CTRT { CD_EMP = 1, CD_FIL = 2, SQ_CTR = 2, SQ_PES_CLI = 102, SQ_PES_CRO = 900, DT_EXC_API = DateTime.Today },
			new T009CTRT { CD_EMP = 1, CD_FIL = 1, SQ_CTR = 3, SQ_PES_CLI = 103, SQ_PES_CRO = 900 });

		db.SaveChanges();
		return db;
	}

	private static string[] Visiveis(EscopoConsulta escopo)
	{
		using var db = CriarContexto();

		return AcessoContrato.AplicarEscopo(AcessoContrato.Validos(db, 1), escopo)
			.OrderBy(c => c.CD_FIL).ThenBy(c => c.SQ_CTR)
			.AsEnumerable()
			.Select(c => $"{c.CD_FIL}/{c.SQ_CTR}")
			.ToArray();
	}

	[Fact]
	public void AcessoTotal_NaoVeExcluidosNemDeAssociadoExcluido()
	{
		Assert.Equal(new[] { "1/1", "1/2", "2/1" }, Visiveis(EscopoConsulta.Total));
	}

	[Fact]
	public void SemAcesso_NaoVeNada()
	{
		Assert.Empty(Visiveis(EscopoConsulta.Nenhum));
	}

	[Fact]
	public void Filial_VeSoAFilial()
	{
		Assert.Equal(new[] { "1/1", "1/2" }, Visiveis(EscopoConsulta.Criar([new EscopoAcesso(1, 1, null)])));
	}

	[Fact]
	public void Carteira_VeSoOsContratosDoCorretor()
	{
		Assert.Equal(new[] { "1/1", "2/1" }, Visiveis(EscopoConsulta.Criar([new EscopoAcesso(1, null, 900)])));
	}

	[Fact]
	public void CarteiraNaFilial_VeSoOsContratosDoCorretorNaFilial()
	{
		Assert.Equal(new[] { "2/1" }, Visiveis(EscopoConsulta.Criar([new EscopoAcesso(1, 2, 900)])));
	}

	[Fact]
	public void EscoposSeSomam()
	{
		Assert.Equal(new[] { "1/2", "2/1" }, Visiveis(EscopoConsulta.Criar(
			[new EscopoAcesso(1, 2, 900), new EscopoAcesso(1, null, 901)])));
	}

	[Fact]
	public async Task Carregar_ForaDoEscopo_DevolveNulo()
	{
		await using var db = CriarContexto();
		var carteira = EscopoConsulta.Criar([new EscopoAcesso(1, null, 901)]);

		Assert.Null(await AcessoContrato.CarregarAsync(db, 1, 1, 1, carteira, false, CancellationToken.None));
		Assert.NotNull(await AcessoContrato.CarregarAsync(db, 1, 1, 2, carteira, false, CancellationToken.None));
		Assert.Null(await AcessoContrato.CarregarAsync(db, 1, 2, 2, EscopoConsulta.Total, false, CancellationToken.None));
	}

	[Fact]
	public async Task Sincronizar_AcertaQuantidade_EUltimoDescontoNoProduto2()
	{
		await using var db = CriarContexto();

		db.Parcelas.AddRange(
			Parcela(1, 1, 1, new DateTime(2027, 1, 10)),
			Parcela(1, 1, 2, new DateTime(2027, 3, 10)),
			Parcela(1, 1, 3, new DateTime(2027, 2, 10)),
			Parcela(1, 2, 1, new DateTime(2027, 5, 10)));
		await db.SaveChangesAsync();

		var produto2 = await db.Contratos.SingleAsync(c => c.CD_FIL == 1 && c.SQ_CTR == 1);
		var produto1 = await db.Contratos.SingleAsync(c => c.CD_FIL == 1 && c.SQ_CTR == 2);

		await AcessoContrato.SincronizarParcelasAsync(db, produto2, CancellationToken.None);
		await AcessoContrato.SincronizarParcelasAsync(db, produto1, CancellationToken.None);

		Assert.Equal(3, produto2.QT_PCL);
		Assert.Equal(new DateTime(2027, 3, 10), produto2.DT_ULT_DCT);
		Assert.Equal(1, produto1.QT_PCL);
		Assert.Null(produto1.DT_ULT_DCT);
	}

	private static T009PAGA Parcela(decimal fil, decimal ctr, decimal nr, DateTime vencimento) => new()
	{
		CD_EMP = 1, CD_FIL = fil, SQ_CTR = ctr, NR_PCL = nr, DT_VEN = vencimento, VR_PCL = 100
	};
}
