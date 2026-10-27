using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.ContasBancarias;
using Xunit;

namespace SigeraAssociados.UnitTests;

/// <summary>
/// Testa as regras de formato da conta bancária e do banco.
/// </summary>
public sealed class ValidacaoContaBancariaTests
{
	private static ContaBancariaDados Valida(
		decimal? cdBanco = 104,
		decimal? agencia = 1234,
		decimal? numeroConta = 123456,
		string? digitoConta = "7",
		string? operacao = "013") => new()
	{
		CdBanco = cdBanco,
		Agencia = agencia,
		NumeroConta = numeroConta,
		DigitoConta = digitoConta,
		Operacao = operacao
	};

	[Fact]
	public void ContaCompleta_DeveSerValida()
	{
		Assert.Null(ValidacaoContaBancaria.Validar(Valida()));
	}

	[Fact]
	public void AgenciaZero_DeveSerAceita()
	{
		Assert.Null(ValidacaoContaBancaria.Validar(Valida(agencia: 0)));
	}

	[Fact]
	public void SemBanco_DeveSerRecusada()
	{
		Assert.Contains("cdBanco", ValidacaoContaBancaria.Validar(Valida(cdBanco: null)));
	}

	[Fact]
	public void SemAgencia_DeveSerRecusada()
	{
		Assert.Contains("agência", ValidacaoContaBancaria.Validar(Valida(agencia: null)));
	}

	[Fact]
	public void SemNumero_DeveSerRecusada()
	{
		Assert.Contains("numeroConta", ValidacaoContaBancaria.Validar(Valida(numeroConta: null)));
	}

	[Fact]
	public void AgenciaComMaisDe5Digitos_DeveSerRecusada()
	{
		Assert.Contains("agencia", ValidacaoContaBancaria.Validar(Valida(agencia: 123456)));
	}

	[Fact]
	public void ContaComMaisDe13Digitos_DeveSerRecusada()
	{
		Assert.Contains("numeroConta", ValidacaoContaBancaria.Validar(Valida(numeroConta: 12_345_678_901_234m)));
	}

	[Fact]
	public void DigitoDaContaComMaisDe2_DeveSerRecusado()
	{
		Assert.Contains("digitoConta", ValidacaoContaBancaria.Validar(Valida(digitoConta: "123")));
	}

	[Fact]
	public void OperacaoComMaisDe5_DeveSerRecusada()
	{
		Assert.Contains("operacao", ValidacaoContaBancaria.Validar(Valida(operacao: "123456")));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public void TipoDeConta_ValoresFixos_DevemSerAceitos(int tipo)
	{
		Assert.Null(ValidacaoContaBancaria.Validar(new ContaBancariaDados { CdBanco = 104, Agencia = 1, NumeroConta = 1, CdTipoConta = tipo }));
	}

	[Fact]
	public void TipoDeConta_Desconhecido_DeveSerRecusado()
	{
		Assert.Contains("POUPANÇA", ValidacaoContaBancaria.Validar(new ContaBancariaDados { CdBanco = 104, Agencia = 1, NumeroConta = 1, CdTipoConta = 3 }));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(3)]
	public void SituacaoDaConta_DiferenteDe1e2_DeveSerRecusada(int situacao)
	{
		Assert.Contains("INATIVA", ValidacaoContaBancaria.Validar(new ContaBancariaDados { CdBanco = 104, Agencia = 1, NumeroConta = 1, CdSituacao = situacao }));
	}

	[Fact]
	public void RegrasDaConta_DescricoesECodigos()
	{
		Assert.Equal("CONTA CORRENTE", RegrasContaBancaria.DescricaoTipo(1));
		Assert.Equal("SEM CONTA", RegrasContaBancaria.DescricaoTipo(0));
		Assert.Equal("INATIVA", RegrasContaBancaria.DescricaoSituacao(2));
		Assert.Equal(2m, RegrasContaBancaria.CodigoOptanteCef(true));
		Assert.Equal(1m, RegrasContaBancaria.CodigoOptanteCef(false));
		Assert.Null(RegrasContaBancaria.CodigoOptanteCef(null));
		Assert.True(RegrasContaBancaria.EnviarOptanteCef(2));
		Assert.False(RegrasContaBancaria.EnviarOptanteCef(1));
		Assert.Null(RegrasContaBancaria.EnviarOptanteCef(0));
	}

	[Fact]
	public void Banco_Valido()
	{
		Assert.Null(ValidacaoTabelasApoio.Banco(new BancoDados { CdBanco = 104, Nome = "CAIXA" }, inclusao: true));
	}

	[Theory]
	[InlineData(2)]
	[InlineData(9)]
	public void Banco_SituacaoDiferenteDe0e1_DeveSerRecusada(int situacao)
	{
		Assert.Contains("0 (INATIVO)",
			ValidacaoTabelasApoio.Banco(new BancoDados { CdBanco = 104, Nome = "CAIXA", CdSituacao = situacao }, inclusao: true));
	}

	[Fact]
	public void Banco_SituacaoDescricao()
	{
		Assert.Equal("ATIVO", SigeraAssociados.Application.Orgaos.SituacaoAtivoInativo.Banco.Descricao(1));
		Assert.Equal("INATIVO", SigeraAssociados.Application.Orgaos.SituacaoAtivoInativo.Banco.Descricao(0));
	}

	[Fact]
	public void Banco_SemCodigoNaInclusao_DeveSerRecusado()
	{
		Assert.Contains("cdBanco", ValidacaoTabelasApoio.Banco(new BancoDados { Nome = "CAIXA" }, inclusao: true));
	}

	[Fact]
	public void Banco_NomeComMaisDe35_DeveSerRecusado()
	{
		Assert.Contains("35", ValidacaoTabelasApoio.Banco(new BancoDados { CdBanco = 1, Nome = new string('A', 36) }, inclusao: true));
	}
}
