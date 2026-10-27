using System.Net.Mail;
using SigeraAssociados.Application.Associados;

namespace SigeraAssociados.Application.Pix;

/// <summary>
/// Dados de inclusão e alteração de uma chave Pix do associado (T009PIX).
/// Na alteração, a sequência vem da rota.
/// </summary>
public sealed class ChavePixDados
{
	/// <summary>
	/// Tipo da chave (CD_PIX): 1 = CPF/CNPJ, 2 = CELULAR, 3 = E-MAIL, 4 = ALEATÓRIA. Obrigatório.
	/// </summary>
	/// <example>1</example>
	public short? CdTipo { get; init; }

	/// <summary>
	/// Chave (VL_CHAVE). CPF/CNPJ e celular aceitam pontuação e são gravados só
	/// com os números; e-mail e aleatória são gravados em minúsculas. Obrigatória.
	/// </summary>
	/// <example>123.456.789-09</example>
	public string? Chave { get; init; }
}

/// <summary>
/// Chave Pix do associado.
/// </summary>
public sealed class ChavePixDto
{
	/// <summary>Sequência do registro da pessoa (SQ_REG).</summary>
	public decimal SqPix { get; init; }

	public short CdTipo { get; init; }

	/// <summary>CPF/CNPJ, CELULAR, E-MAIL ou ALEATÓRIA.</summary>
	public string? Tipo { get; init; }

	/// <summary>Chave como está gravada.</summary>
	public string Chave { get; init; } = string.Empty;

	/// <summary>Chave formatada para exibição (CPF, CNPJ e celular com pontuação).</summary>
	public string Formatada { get; init; } = string.Empty;
}

/// <summary>
/// Tipos de chave Pix (T009PIX.CD_PIX): valores fixos do legado, sem tabela.
/// </summary>
public static class TipoChavePix
{
	public const short CpfCnpj = 1;
	public const short Celular = 2;
	public const short Email = 3;
	public const short Aleatoria = 4;

	public static string? Descricao(short? codigo) => codigo switch
	{
		CpfCnpj => "CPF/CNPJ",
		Celular => "CELULAR",
		Email => "E-MAIL",
		Aleatoria => "ALEATÓRIA",
		_ => null
	};

	public static bool Valido(short? codigo) => codigo is >= CpfCnpj and <= Aleatoria;
}

/// <summary>
/// Regras da chave Pix do associado (sem acesso ao banco).
/// </summary>
public static class ValidacaoChavePix
{
	/// <summary>
	/// Quantidade máxima de chaves por pessoa (SQ_REG é numeric(2,0)).
	/// </summary>
	public const int MaximoChaves = 99;

	/// <summary>Tamanho máximo do e-mail como chave Pix (regra do Banco Central).</summary>
	public const int TamanhoMaximoEmail = 77;

	public static string? Validar(ChavePixDados? dados)
	{
		if (dados is null)
		{
			return "Informe os dados da chave Pix.";
		}

		if (dados.CdTipo is null)
		{
			return "O tipo da chave (cdTipo) é obrigatório.";
		}

		if (!TipoChavePix.Valido(dados.CdTipo))
		{
			return "O campo cdTipo deve ser 1 (CPF/CNPJ), 2 (CELULAR), 3 (E-MAIL) ou 4 (ALEATÓRIA).";
		}

		if (string.IsNullOrWhiteSpace(dados.Chave))
		{
			return "A chave Pix (chave) é obrigatória.";
		}

		var chave = dados.Chave.Trim();

		return dados.CdTipo switch
		{
			TipoChavePix.CpfCnpj => ValidarCpfCnpj(chave),
			TipoChavePix.Celular => ValidarCelular(chave),
			TipoChavePix.Email => ValidarEmail(chave),
			_ => ValidarAleatoria(chave)
		};
	}

	/// <summary>
	/// Forma gravada da chave: só números (CPF/CNPJ), DDD + número sem o +55
	/// (celular) ou minúsculas (e-mail e aleatória). Chamar depois de Validar.
	/// </summary>
	public static string Normalizar(short tipo, string chave)
	{
		var texto = chave.Trim();

		switch (tipo)
		{
			case TipoChavePix.CpfCnpj:
				return Digitos(texto);

			case TipoChavePix.Celular:
				var digitos = Digitos(texto);
				return digitos.Length == 13 && digitos.StartsWith("55", StringComparison.Ordinal) ? digitos[2..] : digitos;

			default:
				return texto.ToLowerInvariant();
		}
	}

	/// <summary>
	/// Chave para exibição. Dado antigo fora do formato volta como está.
	/// </summary>
	public static string Formatar(short tipo, string chave)
	{
		var texto = chave.Trim();
		var d = Digitos(texto);

		return tipo switch
		{
			TipoChavePix.CpfCnpj when d.Length == 11 && d == texto =>
				$"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}",
			TipoChavePix.CpfCnpj when d.Length == 14 && d == texto =>
				$"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}",
			TipoChavePix.Celular when d.Length == 11 && d == texto =>
				$"({d[..2]}) {d[2..7]}-{d[7..]}",
			_ => texto
		};
	}

	private static string? ValidarCpfCnpj(string chave)
	{
		if (!SoPontuacao(chave, '.', '-', '/', ' '))
		{
			return "A chave CPF/CNPJ deve ter só números (pontuação é aceita).";
		}

		var d = Digitos(chave);

		return d.Length switch
		{
			11 => DocumentoCpf.Valido(d) ? null : "CPF inválido.",
			14 => CnpjValido(d) ? null : "CNPJ inválido.",
			_ => "A chave CPF/CNPJ deve ter 11 (CPF) ou 14 (CNPJ) números."
		};
	}

	private static string? ValidarCelular(string chave)
	{
		if (!SoPontuacao(chave, '+', '(', ')', '-', '.', ' '))
		{
			return "A chave celular deve ter só números (pontuação e +55 são aceitos).";
		}

		var d = Digitos(chave);

		if (d.Length == 13 && d.StartsWith("55", StringComparison.Ordinal))
		{
			d = d[2..];
		}

		if (d.Length != 11 || d[2] != '9' || int.Parse(d[..2]) < 11)
		{
			return "A chave celular deve ter DDD + 9 números, começando por 9 (ex.: 88 99999-1234).";
		}

		return null;
	}

	private static string? ValidarEmail(string chave)
	{
		if (chave.Length > TamanhoMaximoEmail)
		{
			return $"A chave e-mail aceita no máximo {TamanhoMaximoEmail} caracteres.";
		}

		if (chave.Contains(' ') || !MailAddress.TryCreate(chave, out var email) || email.Address != chave
			|| !email.Host.Contains('.'))
		{
			return "A chave e-mail não é um endereço válido.";
		}

		return null;
	}

	private static string? ValidarAleatoria(string chave) =>
		chave.Length == 36 && Guid.TryParseExact(chave, "D", out _)
			? null
			: "A chave aleatória deve ter 36 caracteres no formato 123e4567-e89b-12d3-a456-426614174000.";

	private static string Digitos(string texto) => new(texto.Where(char.IsAsciiDigit).ToArray());

	private static bool SoPontuacao(string texto, params char[] permitidos) =>
		texto.All(ch => char.IsAsciiDigit(ch) || permitidos.Contains(ch));

	private static bool CnpjValido(string d)
	{
		if (d.Distinct().Count() == 1)
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
				soma += (d[i] - '0') * pesos[i];
			}

			var resto = soma % 11;

			return resto < 2 ? 0 : 11 - resto;
		}

		return Digito(12) == d[12] - '0' && Digito(13) == d[13] - '0';
	}
}
