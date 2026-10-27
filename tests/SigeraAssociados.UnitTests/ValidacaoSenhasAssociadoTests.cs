using SigeraAssociados.Application.Associados;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras das senhas do portal (T000CLIE.NR_PWS) e de averbação (NR_PWS_02).
/// </summary>
public sealed class ValidacaoSenhasAssociadoTests
{
	private static CadastroAssociadoDados ComSenhas(string? senhaPortal, string? senhaAverbacao = null) => new()
	{
		Nome = "MARIA DA SILVA",
		PessoaFisica = new PessoaFisicaDados
		{
			Cpf = "529.982.247-25",
			DataNascimento = new DateTime(1980, 5, 10),
			Sexo = "F"
		},
		Associado = new AssociadoDados { CdFil = 1, CdSitCli = 1, SenhaPortal = senhaPortal, SenhaAverbacao = senhaAverbacao }
	};

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("1234")]
	[InlineData("Senha@2026!")]
	[InlineData(" com espaço ")]
	[InlineData("çãoÁÉ")]
	public void SenhaAceita_DeveSerValida(string? senha)
	{
		Assert.Null(ValidacaoCadastroAssociado.Validar(ComSenhas(senha, senha)));
	}

	[Fact]
	public void SenhaCom50Caracteres_DeveSerValida()
	{
		Assert.Null(ValidacaoCadastroAssociado.Validar(ComSenhas(new string('x', 50))));
	}

	[Fact]
	public void SenhaPortalComMaisDe50_DeveSerRecusada()
	{
		Assert.Contains("senhaPortal", ValidacaoCadastroAssociado.Validar(ComSenhas(new string('x', 51))));
	}

	[Fact]
	public void SenhaAverbacaoComMaisDe50_DeveSerRecusada()
	{
		Assert.Contains("senhaAverbacao", ValidacaoCadastroAssociado.Validar(ComSenhas("1234", new string('x', 51))));
	}

	[Theory]
	[InlineData("abc\tdef")]
	[InlineData("linha\nquebrada")]
	[InlineData("emoji😀")]
	[InlineData("€uro")]
	public void SenhaComCaractereQueOVarcharNaoGuarda_DeveSerRecusada(string senha)
	{
		Assert.Contains("senha", ValidacaoCadastroAssociado.Validar(ComSenhas(senha)));
	}
}
