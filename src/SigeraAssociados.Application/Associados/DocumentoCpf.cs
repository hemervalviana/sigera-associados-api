using System.Globalization;

namespace SigeraAssociados.Application.Associados;

/// <summary>
/// Regras do CPF: normalização, validação dos dígitos verificadores
/// e máscara para exibição (LGPD).
/// No banco, T000PFIS.NR_CPF é numeric(11,0), sem os zeros à esquerda.
/// </summary>
public static class DocumentoCpf
{
	/// <summary>
	/// Mantém só os números ("123.456.789-09" vira "12345678909").
	/// Retorna null quando não sobra nenhum número.
	/// </summary>
	public static string? SomenteDigitos(string? valor)
	{
		if (string.IsNullOrWhiteSpace(valor))
		{
			return null;
		}

		var digitos = new string(valor.Where(char.IsAsciiDigit).ToArray());

		return digitos.Length == 0 ? null : digitos;
	}

	/// <summary>
	/// Confere tamanho (11 números), sequências repetidas e os dois
	/// dígitos verificadores.
	/// </summary>
	public static bool Valido(string? digitos)
	{
		if (digitos is null || digitos.Length != 11 || !digitos.All(char.IsAsciiDigit))
		{
			return false;
		}

		if (digitos.Distinct().Count() == 1)
		{
			return false;
		}

		return DigitoVerificador(digitos, 9) == digitos[9] - '0'
			&& DigitoVerificador(digitos, 10) == digitos[10] - '0';
	}

	/// <summary>
	/// Máscara para exibição: 12345678909 vira ***.456.789-**.
	/// </summary>
	public static string? Mascarar(decimal? numero)
	{
		if (numero is null || numero <= 0)
		{
			return null;
		}

		var texto = decimal.Truncate(numero.Value).ToString("00000000000", CultureInfo.InvariantCulture);

		return texto.Length != 11
			? null
			: $"***.{texto[3..6]}.{texto[6..9]}-**";
	}

	private static int DigitoVerificador(string digitos, int quantidade)
	{
		var soma = 0;

		for (var i = 0; i < quantidade; i++)
		{
			soma += (digitos[i] - '0') * (quantidade + 1 - i);
		}

		var resto = soma % 11;

		return resto < 2 ? 0 : 11 - resto;
	}
}
