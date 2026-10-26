using SigeraAssociados.Application.Acesso;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa o escopo de acesso aplicado às gravações de associados.
/// </summary>
public sealed class EscopoEscritaTests
{
	private static readonly decimal[] SemCorretor = [];

	private static EscopoConsulta Escopo(params EscopoAcesso[] itens) => EscopoConsulta.Criar(itens);

	[Fact]
	public void AcessoTotal_PermiteTudo()
	{
		Assert.True(EscopoEscrita.PermiteAssociado(EscopoConsulta.Total, null, SemCorretor));
		Assert.True(EscopoEscrita.AvaliarInclusao(EscopoConsulta.Total, 7, null).Permitido);
	}

	[Fact]
	public void AcessoPorFilial_PermiteSoAFilial()
	{
		var escopo = Escopo(new EscopoAcesso(2, 1, null));

		Assert.True(EscopoEscrita.PermiteAssociado(escopo, 1, SemCorretor));
		Assert.False(EscopoEscrita.PermiteAssociado(escopo, 3, SemCorretor));
		Assert.False(EscopoEscrita.PermiteAssociado(escopo, null, SemCorretor));

		Assert.True(EscopoEscrita.AvaliarInclusao(escopo, 1, null).Permitido);
		Assert.False(EscopoEscrita.AvaliarInclusao(escopo, 3, null).Permitido);
	}

	[Fact]
	public void Corretor_VeSoOsDele()
	{
		var escopo = Escopo(new EscopoAcesso(2, null, 900));

		Assert.True(EscopoEscrita.PermiteAssociado(escopo, 1, [900m]));
		Assert.False(EscopoEscrita.PermiteAssociado(escopo, 1, [901m]));
		Assert.False(EscopoEscrita.PermiteAssociado(escopo, 1, SemCorretor));
	}

	[Fact]
	public void Corretor_AoIncluir_VinculaOCorretor()
	{
		var escopo = Escopo(new EscopoAcesso(2, null, 900));

		var avaliacao = EscopoEscrita.AvaliarInclusao(escopo, 1, null);

		Assert.True(avaliacao.Permitido);
		Assert.Equal(900m, avaliacao.Corretor);
	}

	[Fact]
	public void CorretorNaFilial_NaoIncluiEmOutraFilial()
	{
		var escopo = Escopo(new EscopoAcesso(2, 1, 900));

		Assert.True(EscopoEscrita.AvaliarInclusao(escopo, 1, null).Permitido);

		var outra = EscopoEscrita.AvaliarInclusao(escopo, 3, null);
		Assert.False(outra.Permitido);
		Assert.False(outra.FaltaEscolherCorretor);
	}

	[Fact]
	public void DoisCorretores_ExigemEscolha()
	{
		var escopo = Escopo(new EscopoAcesso(2, null, 900), new EscopoAcesso(2, null, 901));

		var semEscolha = EscopoEscrita.AvaliarInclusao(escopo, 1, null);
		Assert.False(semEscolha.Permitido);
		Assert.True(semEscolha.FaltaEscolherCorretor);

		var comEscolha = EscopoEscrita.AvaliarInclusao(escopo, 1, 901);
		Assert.True(comEscolha.Permitido);
		Assert.Equal(901m, comEscolha.Corretor);

		Assert.False(EscopoEscrita.AvaliarInclusao(escopo, 1, 555).Permitido);
	}

	[Fact]
	public void SemAcesso_NaoPermite()
	{
		Assert.False(EscopoEscrita.PermiteAssociado(EscopoConsulta.Nenhum, 1, SemCorretor));
		Assert.False(EscopoEscrita.AvaliarInclusao(EscopoConsulta.Nenhum, 1, null).Permitido);
	}
}
