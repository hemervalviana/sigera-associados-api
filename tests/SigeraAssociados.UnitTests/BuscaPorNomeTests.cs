using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Repositories;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa a busca de associados pelo começo do nome.
/// </summary>
public sealed class BuscaPorNomeTests
{
	private static string[] Buscar(string? nome)
	{
		var opcoes = new DbContextOptionsBuilder<SigeraDbContext>()
			.UseInMemoryDatabase($"nome-{Guid.NewGuid()}")
			.Options;

		using var db = new SigeraDbContext(opcoes);

		var nomes = new[] { "MARIA DA SILVA", "MARIANA SOUZA", "ANA MARIA LIMA", "JOSE SILVA" };

		for (var i = 0; i < nomes.Length; i++)
		{
			db.Pessoas.Add(new T000PESS
			{
				CD_EMP = 2,
				SQ_PES = i + 1,
				TP_PES = "F",
				NM_PES = nomes[i],
				DT_CAD = new DateTime(2020, 1, 1)
			});

			db.Associados.Add(new T000CLIE { CD_EMP = 2, SQ_PES_CLI = i + 1 });
		}

		db.SaveChanges();

		return AssociadosRepository
			.FiltrarPorNome(db.Associados.Include(a => a.Pessoa), nome)
			.AsEnumerable()
			.Select(a => a.Pessoa!.NM_PES)
			.OrderBy(n => n)
			.ToArray();
	}

	[Fact]
	public void Nome_DeveEncontrarSomenteQuemComecaComOTexto()
	{
		Assert.Equal(new[] { "MARIA DA SILVA", "MARIANA SOUZA" }, Buscar("MARIA"));
	}

	[Fact]
	public void NomeNoMeio_NaoDeveSerEncontrado()
	{
		Assert.Empty(Buscar("SILVA"));
	}

	[Fact]
	public void Nome_ComEspacosNasPontas_DeveSerAjustado()
	{
		Assert.Equal(new[] { "JOSE SILVA" }, Buscar("  JOSE "));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SemNome_NaoDeveFiltrar(string? nome)
	{
		Assert.Equal(4, Buscar(nome).Length);
	}
}
