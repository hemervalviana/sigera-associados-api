namespace SigeraAssociados.Application.Pessoas;

/// <summary>
/// Pessoa já cadastrada (achada pelo CPF ou CNPJ) que pode ganhar outro papel:
/// associado que vira corretor, ou corretor que vira associado. A pessoa
/// (T000PESS + T000PFIS/T000PJUR) é aproveitada e a API cria só o registro
/// do papel novo (T009CRRT ou T000CLIE).
/// </summary>
public static class RegrasPessoaExistente
{
	/// <summary>O que já existe para a pessoa achada pelo documento.</summary>
	public sealed record Situacao(
		decimal SqPes,
		string? TipoPessoa,
		bool TemFisica,
		bool TemJuridica,
		bool EhAssociado,
		bool AssociadoExcluido,
		bool EhCorretor,
		bool CorretorExcluido);

	/// <summary>
	/// Pode criar o T009CRRT para a pessoa? Null = pode; senão, a recusa.
	/// </summary>
	public static string? PodeVirarCorretor(Situacao s, string tipoPessoa)
	{
		if (s.EhCorretor)
		{
			return s.CorretorExcluido
				? $"Esta pessoa (código {s.SqPes:0}) é um corretor excluído pela API."
				: $"Já existe um corretor com este documento (código {s.SqPes:0}).";
		}

		return Coerente(s, tipoPessoa);
	}

	/// <summary>
	/// Pode criar o T000CLIE para a pessoa? Null = pode; senão, a recusa.
	/// O associado é sempre pessoa física.
	/// </summary>
	public static string? PodeVirarAssociado(Situacao s)
	{
		if (s.EhAssociado)
		{
			return s.AssociadoExcluido
				? $"Este CPF é de um associado excluído pela API (código {s.SqPes:0})."
				: $"Já existe um associado com este CPF (código {s.SqPes:0}).";
		}

		return Coerente(s, "F");
	}

	/// <summary>
	/// Física com T000PFIS e sem T000PJUR; jurídica o contrário.
	/// </summary>
	private static string? Coerente(Situacao s, string tipoPessoa)
	{
		var coerente = tipoPessoa == "F"
			? s.TipoPessoa == "F" && s.TemFisica && !s.TemJuridica
			: s.TipoPessoa == "J" && s.TemJuridica && !s.TemFisica;

		return coerente
			? null
			: $"A pessoa {s.SqPes:0} com este documento tem cadastro incoerente no sistema " +
			  $"(tipo {s.TipoPessoa ?? "vazio"}); corrija no sistema legado antes de aproveitá-la.";
	}
}
