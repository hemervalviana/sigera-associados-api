using SigeraAssociados.Application.Usuarios;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa os nomes oficiais dos grupos e as combinações de autorização.
/// </summary>
public sealed class GruposTests
{
	[Theory]
	[InlineData("Corretores", "Corretores")]
	[InlineData("corretores", "Corretores")]
	[InlineData("  GESTORES  ", "Gestores")]
	[InlineData("coordenadora adm. & operacional", "Coordenadora Adm. & Operacional")]
	public void Normalizar_GrupoValido_DeveDevolverNomeOficial(string informado, string esperado)
	{
		Assert.Equal(esperado, Grupos.Normalizar(informado));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("Diretoria")]
	[InlineData("Administrador")]
	public void Normalizar_GrupoInvalido_DeveDevolverNull(string? informado)
	{
		Assert.Null(Grupos.Normalizar(informado));
	}

	[Fact]
	public void Funcionais_DeveTerOsSeteGruposOficiais()
	{
		Assert.Equal(7, Grupos.Funcionais.Count);
		Assert.Contains(Grupos.Administradores, Grupos.Funcionais);
		Assert.Contains(Grupos.Gestores, Grupos.Funcionais);
		Assert.DoesNotContain(Grupos.AdministradorLegado, Grupos.Funcionais);
	}

	[Theory]
	[InlineData("Administradores")]
	[InlineData("Administrador")]
	[InlineData("administradores")]
	public void ContemAdministrador_ComGrupoAdministrativo_DeveSerVerdadeiro(string funcao)
	{
		Assert.True(Grupos.ContemAdministrador(new[] { "Corretores", funcao }));
	}

	[Fact]
	public void ContemAdministrador_SemGrupoAdministrativo_DeveSerFalso()
	{
		Assert.False(Grupos.ContemAdministrador(new[] { "Gestores", "Corretores" }));
		Assert.False(Grupos.ContemAdministrador(Array.Empty<string>()));
	}

	[Fact]
	public void PerfisGestaoUsuarios_DeveIncluirGestoresEAdministradores()
	{
		var perfis = Grupos.PerfisGestaoUsuarios.Split(',');

		Assert.Equal(
			new[] { "Administrador", "Administradores", "Gestores" },
			perfis.OrderBy(p => p).ToArray());
	}

	[Fact]
	public void PerfisAdministracao_NaoDeveIncluirGestores()
	{
		Assert.DoesNotContain(Grupos.Gestores, Grupos.PerfisAdministracao.Split(','));
	}
}
