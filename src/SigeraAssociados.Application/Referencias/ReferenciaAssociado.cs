using SigeraAssociados.Application.Cadastros;

namespace SigeraAssociados.Application.Referencias;

/// <summary>
/// Dados de inclusão e alteração de uma referência pessoal do associado (T000REFE).
/// Na alteração, a sequência vem da rota.
/// </summary>
public sealed class ReferenciaAssociadoDados
{
	/// <summary>Nome (NM_REF, até 50). Obrigatório.</summary>
	/// <example>MARIA DA SILVA</example>
	public string? Nome { get; init; }

	/// <summary>Data de nascimento (DT_NAS_REF). Opcional.</summary>
	public DateTime? DataNascimento { get; init; }

	/// <summary>
	/// Telefone 1 (NR_FN1_REF): DDD + número, 10 ou 11 números, com ou sem
	/// pontuação. Gravado só com os números. Obrigatório.
	/// </summary>
	/// <example>(88) 99999-1234</example>
	public string? Telefone1 { get; init; }

	/// <summary>Telefone 2 (NR_FN2_REF), no mesmo formato. Opcional.</summary>
	public string? Telefone2 { get; init; }
}

/// <summary>
/// Referência pessoal do associado.
/// </summary>
public sealed class ReferenciaAssociadoDto
{
	/// <summary>Sequência da referência (SQ_REF).</summary>
	public decimal SqReferencia { get; init; }

	public string Nome { get; init; } = string.Empty;

	public DateTime? DataNascimento { get; init; }

	/// <summary>Telefone 1 como está gravado.</summary>
	public string Telefone1 { get; init; } = string.Empty;

	/// <summary>Telefone 1 formatado, ex.: (88) 99999-1234.</summary>
	public string Telefone1Formatado { get; init; } = string.Empty;

	public string? Telefone2 { get; init; }

	public string? Telefone2Formatado { get; init; }
}

/// <summary>
/// Regras da referência do associado (sem acesso ao banco).
/// </summary>
public static class ValidacaoReferenciaAssociado
{
	/// <summary>
	/// Quantidade máxima de referências por pessoa (SQ_REF é numeric(4,0)).
	/// </summary>
	public const int MaximoReferencias = 9999;

	// Faixa aceita pelo tipo datetime do legado, limitada ao passado.
	private static readonly DateTime DataMinima = new(1900, 1, 1);

	public static string? Validar(ReferenciaAssociadoDados? dados)
	{
		if (dados is null)
		{
			return "Informe os dados da referência.";
		}

		if (string.IsNullOrWhiteSpace(dados.Nome))
		{
			return "O nome da referência é obrigatório.";
		}

		if (dados.Nome.Trim().Length > 50)
		{
			return "O campo nome aceita no máximo 50 caracteres.";
		}

		if (dados.DataNascimento is DateTime nascimento
			&& (nascimento.Date > DateTime.Today || nascimento < DataMinima))
		{
			return "O campo dataNascimento deve ser uma data entre 01/01/1900 e hoje.";
		}

		if (string.IsNullOrWhiteSpace(dados.Telefone1))
		{
			return "O telefone 1 (telefone1) é obrigatório.";
		}

		return Telefone(dados.Telefone1, "telefone1")
			?? (string.IsNullOrWhiteSpace(dados.Telefone2) ? null : Telefone(dados.Telefone2, "telefone2"));
	}

	/// <summary>Telefone só com os dígitos, como é gravado; null quando vazio.</summary>
	public static string? Normalizar(string? telefone) =>
		string.IsNullOrWhiteSpace(telefone) ? null : ValidacaoTabelasApoio.SomenteDigitos(telefone);

	/// <summary>
	/// (DDD) NNNN-NNNN ou (DDD) NNNNN-NNNN. Valor fora desse formato
	/// (dado antigo do legado) volta como está.
	/// </summary>
	public static string? Formatar(string? telefone)
	{
		if (string.IsNullOrWhiteSpace(telefone))
		{
			return null;
		}

		var d = ValidacaoTabelasApoio.SomenteDigitos(telefone) ?? string.Empty;

		return d.Length is 10 or 11
			? $"({d[..2]}) {d[2..^4]}-{d[^4..]}"
			: telefone.Trim();
	}

	private static string? Telefone(string valor, string campo)
	{
		var digitos = ValidacaoTabelasApoio.SomenteDigitos(valor) ?? string.Empty;
		var invalidos = valor.Count(ch => !char.IsAsciiDigit(ch) && ch is not ('(' or ')' or '-' or '.' or ' '));

		if (digitos.Length is < 10 or > 11 || invalidos > 0 || int.Parse(digitos[..2]) < 11)
		{
			return $"O campo {campo} deve ter DDD + número (10 ou 11 números), ex.: (88) 99999-1234.";
		}

		return null;
	}
}
