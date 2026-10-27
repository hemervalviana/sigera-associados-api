using SigeraAssociados.Application.Cadastros;

namespace SigeraAssociados.Application.Telefones;

/// <summary>
/// Dados de inclusão e alteração de um telefone do associado (T000TELE).
/// Na alteração, a sequência vem da rota.
/// </summary>
public sealed class TelefoneAssociadoDados
{
	/// <summary>DDD (NR_DDD), de 11 a 99. Obrigatório.</summary>
	/// <example>88</example>
	public decimal? Ddd { get; init; }

	/// <summary>
	/// Número (NR_FN): 8 ou 9 números, com ou sem traço. Gravado só com os números. Obrigatório.
	/// </summary>
	/// <example>99999-1234</example>
	public string? Numero { get; init; }

	/// <summary>
	/// Tipo de telefone (CD_TP_FN), de GET /api/cadastros/tipos-telefone. Opcional.
	/// </summary>
	public decimal? CdTipo { get; init; }
}

/// <summary>
/// Telefone do associado com a descrição do tipo.
/// </summary>
public sealed class TelefoneAssociadoDto
{
	/// <summary>Sequência do telefone (SQ_TELE).</summary>
	public decimal SqTelefone { get; init; }

	public decimal Ddd { get; init; }

	/// <summary>Número como está gravado.</summary>
	public string Numero { get; init; } = string.Empty;

	/// <summary>DDD e número formatados, ex.: (88) 99999-1234.</summary>
	public string Formatado { get; init; } = string.Empty;

	public decimal? CdTipo { get; init; }

	public string? Tipo { get; init; }
}

/// <summary>
/// Regras do telefone do associado (sem acesso ao banco).
/// </summary>
public static class ValidacaoTelefoneAssociado
{
	public static string? Validar(TelefoneAssociadoDados? dados)
	{
		if (dados is null)
		{
			return "Informe os dados do telefone.";
		}

		if (dados.Ddd is null)
		{
			return "O DDD (ddd) é obrigatório.";
		}

		if (dados.Ddd < 11 || dados.Ddd > 99 || dados.Ddd % 1 != 0)
		{
			return "O campo ddd deve ser um número inteiro entre 11 e 99.";
		}

		if (string.IsNullOrWhiteSpace(dados.Numero))
		{
			return "O número do telefone (numero) é obrigatório.";
		}

		var digitos = ValidacaoTabelasApoio.SomenteDigitos(dados.Numero) ?? string.Empty;
		var invalidos = dados.Numero.Count(ch => !char.IsAsciiDigit(ch) && ch is not ('-' or '.' or ' '));

		if (digitos.Length is < 8 or > 9 || invalidos > 0)
		{
			return "O campo numero deve ter 8 ou 9 números (sem o DDD).";
		}

		if (dados.CdTipo is not null && (dados.CdTipo < 1 || dados.CdTipo > 99 || dados.CdTipo % 1 != 0))
		{
			return "O campo cdTipo deve ser um número inteiro entre 1 e 99.";
		}

		return null;
	}

	/// <summary>Número só com os dígitos, como é gravado.</summary>
	public static string Normalizar(string numero) =>
		ValidacaoTabelasApoio.SomenteDigitos(numero) ?? string.Empty;

	/// <summary>
	/// (DDD) NNNN-NNNN ou (DDD) NNNNN-NNNN. Número fora desse formato
	/// (dado antigo do legado) é devolvido como está.
	/// </summary>
	public static string Formatar(decimal ddd, string numero)
	{
		var prefixo = $"({ddd:0})";
		var digitos = ValidacaoTabelasApoio.SomenteDigitos(numero) ?? string.Empty;

		return digitos.Length is 8 or 9
			? $"{prefixo} {digitos[..^4]}-{digitos[^4..]}"
			: $"{prefixo} {numero.Trim()}";
	}
}
