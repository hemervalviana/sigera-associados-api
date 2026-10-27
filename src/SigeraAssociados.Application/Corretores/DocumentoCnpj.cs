using System.Globalization;

namespace SigeraAssociados.Application.Corretores;

/// <summary>
/// Regras do CNPJ: dígitos verificadores e formatação.
/// No banco, T000PJUR.NR_CPJ é numeric(14,0), sem os zeros à esquerda.
/// </summary>
public static class DocumentoCnpj
{
	/// <summary>Só os números; null quando não sobra nenhum.</summary>
	public static string? SomenteDigitos(string? valor)
	{
		if (string.IsNullOrWhiteSpace(valor))
		{
			return null;
		}

		var digitos = new string(valor.Where(char.IsAsciiDigit).ToArray());

		return digitos.Length == 0 ? null : digitos;
	}

	/// <summary>14 números, não todos iguais, com os dois dígitos verificadores certos.</summary>
	public static bool Valido(string? digitos)
	{
		if (digitos is null || digitos.Length != 14 || !digitos.All(char.IsAsciiDigit) || digitos.Distinct().Count() == 1)
		{
			return false;
		}

		int Digito(int tamanho)
		{
			int[] pesos = tamanho == 12
				? [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
				: [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

			var soma = 0;

			for (var i = 0; i < tamanho; i++)
			{
				soma += (digitos[i] - '0') * pesos[i];
			}

			var resto = soma % 11;

			return resto < 2 ? 0 : 11 - resto;
		}

		return Digito(12) == digitos[12] - '0' && Digito(13) == digitos[13] - '0';
	}

	/// <summary>
	/// 11222333000181 → 11.222.333/0001-81, recompondo os zeros à esquerda
	/// perdidos no numeric(14,0).
	/// </summary>
	public static string? Formatar(decimal? numero)
	{
		if (numero is null || numero <= 0)
		{
			return null;
		}

		var t = decimal.Truncate(numero.Value).ToString("00000000000000", CultureInfo.InvariantCulture);

		return t.Length != 14 ? null : $"{t[..2]}.{t[2..5]}.{t[5..8]}/{t[8..12]}-{t[12..]}";
	}
}
