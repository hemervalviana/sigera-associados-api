using SigeraAssociados.Application.Pessoas;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa quando uma pessoa já cadastrada pode ganhar outro papel:
/// associado que vira corretor e corretor que vira associado.
/// </summary>
public sealed class RegrasPessoaExistenteTests
{
	private static RegrasPessoaExistente.Situacao Pessoa(
		string? tipo = "F",
		bool fisica = true,
		bool juridica = false,
		bool associado = false,
		bool associadoExcluido = false,
		bool corretor = false,
		bool corretorExcluido = false) =>
		new(123, tipo, fisica, juridica, associado, associadoExcluido, corretor, corretorExcluido);

	// ----- Associado que vira corretor -----

	[Fact]
	public void AssociadoPodeVirarCorretorPessoaFisica()
	{
		Assert.Null(RegrasPessoaExistente.PodeVirarCorretor(Pessoa(associado: true), "F"));
	}

	[Fact]
	public void PessoaJuridicaSemPapelPodeVirarCorretor()
	{
		Assert.Null(RegrasPessoaExistente.PodeVirarCorretor(Pessoa(tipo: "J", fisica: false, juridica: true), "J"));
	}

	[Fact]
	public void QuemJaECorretor_NaoViraCorretorDeNovo()
	{
		Assert.Contains("Já existe um corretor", RegrasPessoaExistente.PodeVirarCorretor(Pessoa(associado: true, corretor: true), "F"));
	}

	[Fact]
	public void CorretorExcluidoPelaApi_ERecusado()
	{
		Assert.Contains("excluído", RegrasPessoaExistente.PodeVirarCorretor(Pessoa(corretor: true, corretorExcluido: true), "F"));
	}

	// ----- Corretor que vira associado -----

	[Fact]
	public void CorretorPessoaFisicaPodeVirarAssociado()
	{
		Assert.Null(RegrasPessoaExistente.PodeVirarAssociado(Pessoa(corretor: true)));
	}

	[Fact]
	public void QuemJaEAssociado_NaoViraAssociadoDeNovo()
	{
		Assert.Contains("Já existe um associado", RegrasPessoaExistente.PodeVirarAssociado(Pessoa(associado: true, corretor: true)));
	}

	[Fact]
	public void AssociadoExcluidoPelaApi_ERecusado()
	{
		Assert.Contains("excluído", RegrasPessoaExistente.PodeVirarAssociado(Pessoa(associado: true, associadoExcluido: true)));
	}

	// ----- Cadastro incoerente -----

	[Fact]
	public void CorretorPessoaJuridica_NaoViraAssociado()
	{
		Assert.Contains("incoerente",
			RegrasPessoaExistente.PodeVirarAssociado(Pessoa(tipo: "J", fisica: false, juridica: true, corretor: true)));
	}

	[Fact]
	public void PessoaComFisicaEJuridica_NaoEAproveitada()
	{
		Assert.Contains("incoerente", RegrasPessoaExistente.PodeVirarCorretor(Pessoa(fisica: true, juridica: true), "F"));
		Assert.Contains("incoerente", RegrasPessoaExistente.PodeVirarAssociado(Pessoa(fisica: true, juridica: true)));
	}

	[Fact]
	public void TipoDiferenteDoDocumento_NaoEAproveitado()
	{
		// Pessoa marcada como J mas achada pelo CPF.
		Assert.Contains("incoerente", RegrasPessoaExistente.PodeVirarCorretor(Pessoa(tipo: "J"), "F"));
	}
}
