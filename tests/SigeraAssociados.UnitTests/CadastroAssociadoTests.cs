using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Repositories;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa a regra obrigatória do cadastro do associado:
/// T000PESS + T000CLIE + T000PFIS (pessoa física, TP_PES = 'F')
/// ou T000PJUR (pessoa jurídica, TP_PES = 'J').
/// </summary>
public sealed class CadastroAssociadoTests
{
	// Massa de dados (empresa 1):
	// 101 F com PFIS          -> válido
	// 102 J com PJUR          -> válido
	// 103 F sem PFIS          -> inválido
	// 104 J sem PJUR          -> inválido
	// 105 F só com PJUR       -> inválido (tabela errada para o tipo)
	// 106 sem T000PESS        -> inválido
	// 107 tipo 'X' com PFIS   -> inválido
	// 108 F com PFIS de outra empresa -> inválido
	// Empresa 2: 101 F com PFIS -> válido (mesmo código, outra empresa)
	private static SigeraDbContext CriarContexto()
	{
		var opcoes = new DbContextOptionsBuilder<SigeraDbContext>()
			.UseInMemoryDatabase($"cadastro-{Guid.NewGuid()}")
			.Options;

		var db = new SigeraDbContext(opcoes);

		static T000PESS Pessoa(decimal emp, decimal sq, string tipo) => new()
		{
			CD_EMP = emp,
			SQ_PES = sq,
			TP_PES = tipo,
			NM_PES = $"PESSOA {emp}-{sq}",
			DT_CAD = new DateTime(2020, 1, 1)
		};

		db.Pessoas.AddRange(
			Pessoa(1, 101, "F"),
			Pessoa(1, 102, "J"),
			Pessoa(1, 103, "F"),
			Pessoa(1, 104, "J"),
			Pessoa(1, 105, "F"),
			Pessoa(1, 107, "X"),
			Pessoa(1, 108, "F"),
			Pessoa(2, 101, "F"));

		db.Associados.AddRange(
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 101 },
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 102 },
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 103 },
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 104 },
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 105 },
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 106 },
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 107 },
			new T000CLIE { CD_EMP = 1, SQ_PES_CLI = 108 },
			new T000CLIE { CD_EMP = 2, SQ_PES_CLI = 101 });

		db.PessoasFisicas.AddRange(
			new T000PFIS { CD_EMP = 1, SQ_PES_FIS = 101 },
			new T000PFIS { CD_EMP = 1, SQ_PES_FIS = 107 },
			new T000PFIS { CD_EMP = 3, SQ_PES_FIS = 108 },
			new T000PFIS { CD_EMP = 2, SQ_PES_FIS = 101 });

		db.PessoasJuridicas.AddRange(
			new T000PJUR { CD_EMP = 1, SQ_PES_JUR = 102 },
			new T000PJUR { CD_EMP = 1, SQ_PES_JUR = 105 });

		db.SaveChanges();

		return db;
	}

	private static string[] Validos()
	{
		using var db = CriarContexto();

		return AssociadosRepository
			.FiltrarCadastroCompleto(
				db.Associados,
				db.Pessoas,
				db.PessoasFisicas,
				db.PessoasJuridicas)
			.OrderBy(a => a.CD_EMP)
			.ThenBy(a => a.SQ_PES_CLI)
			.AsEnumerable()
			.Select(a => $"{a.CD_EMP}-{a.SQ_PES_CLI}")
			.ToArray();
	}

	[Fact]
	public void SomenteAssociadosComCadastroCompleto_DevemSerConsiderados()
	{
		Assert.Equal(new[] { "1-101", "1-102", "2-101" }, Validos());
	}

	[Theory]
	[InlineData("1-103")] // física sem T000PFIS
	[InlineData("1-104")] // jurídica sem T000PJUR
	[InlineData("1-105")] // física cadastrada só em T000PJUR
	[InlineData("1-106")] // sem T000PESS
	[InlineData("1-107")] // tipo de pessoa inválido
	[InlineData("1-108")] // T000PFIS de outra empresa
	public void AssociadoComCadastroIncompleto_DeveSerExcluido(string associado)
	{
		Assert.DoesNotContain(associado, Validos());
	}
}
