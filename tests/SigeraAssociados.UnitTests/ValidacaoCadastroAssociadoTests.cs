using SigeraAssociados.Application.Associados;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras de formato do cadastro de associado.
/// </summary>
public sealed class ValidacaoCadastroAssociadoTests
{
	private static CadastroAssociadoDados Valido(
		string? nome = "MARIA DA SILVA",
		string? cpf = "529.982.247-25",
		DateTime? nascimento = null,
		string? sexo = "F",
		decimal? cdFil = 1,
		decimal? cdSitCli = 1,
		string? email = null) => new()
	{
		Nome = nome,
		Email = email,
		PessoaFisica = new PessoaFisicaDados
		{
			Cpf = cpf,
			DataNascimento = nascimento ?? new DateTime(1980, 5, 10),
			Sexo = sexo
		},
		Associado = new AssociadoDados { CdFil = cdFil, CdSitCli = cdSitCli }
	};

	[Fact]
	public void CadastroCompleto_DeveSerValido()
	{
		Assert.Null(ValidacaoCadastroAssociado.Validar(Valido()));
	}

	[Fact]
	public void SemDados_DeveSerRecusado()
	{
		Assert.NotNull(ValidacaoCadastroAssociado.Validar(null));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("   ")]
	public void SemNome_DeveSerRecusado(string? nome)
	{
		Assert.Contains("nome", ValidacaoCadastroAssociado.Validar(Valido(nome: nome)));
	}

	[Fact]
	public void NomeComMaisDe80_DeveSerRecusado()
	{
		Assert.Contains("80", ValidacaoCadastroAssociado.Validar(Valido(nome: new string('A', 81))));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("111.111.111-11")]
	[InlineData("123")]
	public void CpfAusenteOuInvalido_DeveSerRecusado(string? cpf)
	{
		Assert.Contains("CPF", ValidacaoCadastroAssociado.Validar(Valido(cpf: cpf)));
	}

	[Fact]
	public void NascimentoNoFuturo_DeveSerRecusado()
	{
		Assert.Contains("nascimento",
			ValidacaoCadastroAssociado.Validar(Valido(nascimento: DateTime.Today.AddDays(1))));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("X")]
	public void SexoInvalido_DeveSerRecusado(string? sexo)
	{
		Assert.Contains("sexo", ValidacaoCadastroAssociado.Validar(Valido(sexo: sexo)));
	}

	[Fact]
	public void SexoMinusculo_DeveSerAceito()
	{
		Assert.Null(ValidacaoCadastroAssociado.Validar(Valido(sexo: "m")));
	}

	[Fact]
	public void SemFilial_DeveSerRecusado()
	{
		Assert.Contains("filial", ValidacaoCadastroAssociado.Validar(Valido(cdFil: null)));
	}

	[Fact]
	public void SemSituacao_DeveSerRecusado()
	{
		Assert.Contains("situação", ValidacaoCadastroAssociado.Validar(Valido(cdSitCli: null)));
	}

	[Fact]
	public void FilialForaDaFaixa_DeveSerRecusada()
	{
		Assert.Contains("cdFil", ValidacaoCadastroAssociado.Validar(Valido(cdFil: 100)));
	}

	[Fact]
	public void EmailSemArroba_DeveSerRecusado()
	{
		Assert.Contains("e-mail", ValidacaoCadastroAssociado.Validar(Valido(email: "maria.exemplo.com")));
	}

	[Fact]
	public void SemPessoaFisica_DeveSerRecusado()
	{
		var dados = new CadastroAssociadoDados
		{
			Nome = "MARIA",
			Associado = new AssociadoDados { CdFil = 1, CdSitCli = 1 }
		};

		Assert.Contains("pessoaFisica", ValidacaoCadastroAssociado.Validar(dados));
	}
}
