using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Erros;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Repositories;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras do CPF e o filtro de associados por CPF.
/// CPFs usados são números de exemplo válidos, não de pessoas reais.
/// </summary>
public sealed class CpfTests
{
	[Theory]
	[InlineData("529.982.247-25", "52998224725")]
	[InlineData("52998224725", "52998224725")]
	[InlineData(" 529 982 247 25 ", "52998224725")]
	public void SomenteDigitos_DeveRemoverPontuacao(string valor, string esperado)
	{
		Assert.Equal(esperado, DocumentoCpf.SomenteDigitos(valor));
	}

	[Theory]
	[InlineData("52998224725")]
	[InlineData("11144477735")]
	public void Valido_ComDigitosCorretos_DeveAceitar(string cpf)
	{
		Assert.True(DocumentoCpf.Valido(cpf));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("52998224724")]   // dígito verificador errado
	[InlineData("5299822472")]    // 10 números
	[InlineData("529982247250")]  // 12 números
	[InlineData("11111111111")]   // sequência repetida
	[InlineData("5299822472a")]
	public void Valido_ComCpfInvalido_DeveRecusar(string? cpf)
	{
		Assert.False(DocumentoCpf.Valido(cpf));
	}

	[Theory]
	[InlineData(52998224725d, "***.982.247-**")]
	[InlineData(1234567890d, "***.345.678-**")]   // zero à esquerda perdido no numeric
	public void Mascarar_DeveEsconderInicioEFim(double numero, string esperado)
	{
		Assert.Equal(esperado, DocumentoCpf.Mascarar((decimal)numero));
	}

	[Fact]
	public void Mascarar_SemCpf_DeveDevolverNull()
	{
		Assert.Null(DocumentoCpf.Mascarar(null));
		Assert.Null(DocumentoCpf.Mascarar(0));
	}

	[Fact]
	public void FiltrarPorCpf_DeveEncontrarSomenteOAssociadoDoCpf()
	{
		var opcoes = new DbContextOptionsBuilder<SigeraDbContext>()
			.UseInMemoryDatabase($"cpf-{Guid.NewGuid()}")
			.Options;

		using var db = new SigeraDbContext(opcoes);

		db.Associados.AddRange(
			new T000CLIE { CD_EMP = 2, SQ_PES_CLI = 10 },
			new T000CLIE { CD_EMP = 2, SQ_PES_CLI = 20 },
			new T000CLIE { CD_EMP = 3, SQ_PES_CLI = 10 });

		db.PessoasFisicas.AddRange(
			new T000PFIS { CD_EMP = 2, SQ_PES_FIS = 10, NR_CPF = 52998224725 },
			new T000PFIS { CD_EMP = 2, SQ_PES_FIS = 20, NR_CPF = 11144477735 },
			new T000PFIS { CD_EMP = 3, SQ_PES_FIS = 10, NR_CPF = 11144477735 });

		db.SaveChanges();

		var encontrados = AssociadosRepository
			.FiltrarPorCpf(db.Associados, db.PessoasFisicas, "52998224725")
			.AsEnumerable()
			.Select(a => $"{a.CD_EMP}-{a.SQ_PES_CLI}")
			.ToArray();

		Assert.Equal(new[] { "2-10" }, encontrados);
		Assert.Equal(3, AssociadosRepository.FiltrarPorCpf(db.Associados, db.PessoasFisicas, null).Count());
	}
}
