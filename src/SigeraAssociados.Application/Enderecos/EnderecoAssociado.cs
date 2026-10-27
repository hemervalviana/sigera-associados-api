using SigeraAssociados.Application.Cadastros;

namespace SigeraAssociados.Application.Enderecos;

/// <summary>
/// Dados de inclusão e alteração de um endereço do associado (T000ENDE).
/// Na alteração, a sequência vem da rota.
/// </summary>
public sealed class EnderecoAssociadoDados
{
	/// <summary>
	/// Tipo de endereço (CD_TP_EN), de GET /api/cadastros/tipos-endereco. Obrigatório.
	/// </summary>
	public decimal? CdTipo { get; init; }

	/// <summary>Logradouro (NM_LOG, até 50). Obrigatório.</summary>
	/// <example>RUA SÃO PEDRO</example>
	public string? Logradouro { get; init; }

	/// <summary>Número (NR_LOG, até 10 dígitos).</summary>
	public decimal? Numero { get; init; }

	/// <summary>Complemento (CP_EN, até 100).</summary>
	public string? Complemento { get; init; }

	/// <summary>Bairro (NM_BAI, até 30).</summary>
	public string? Bairro { get; init; }

	/// <summary>CEP (NR_CEP): 8 números, com ou sem traço.</summary>
	/// <example>63010-000</example>
	public string? Cep { get; init; }

	/// <summary>
	/// Município (CD_MUN), de GET /api/cadastros/municipios. Obrigatório.
	/// </summary>
	public decimal? CdMunicipio { get; init; }

	/// <summary>
	/// Endereço principal (ID_EN_PCP). Ao marcar um endereço como principal,
	/// os outros do associado deixam de ser. O primeiro endereço é sempre principal.
	/// </summary>
	public bool? Principal { get; init; }
}

/// <summary>
/// Endereço do associado com as descrições.
/// </summary>
public sealed class EnderecoAssociadoDto
{
	/// <summary>Sequência do endereço da pessoa (SQ_EN_PES).</summary>
	public decimal SqEndereco { get; init; }

	public decimal CdTipo { get; init; }

	public string? Tipo { get; init; }

	public string Logradouro { get; init; } = string.Empty;

	public decimal? Numero { get; init; }

	public string? Complemento { get; init; }

	public string? Bairro { get; init; }

	/// <summary>CEP com 8 números.</summary>
	public string? Cep { get; init; }

	public decimal CdMunicipio { get; init; }

	public string? Municipio { get; init; }

	public string? Uf { get; init; }

	public bool Principal { get; init; }
}

/// <summary>
/// Regras do endereço do associado (sem acesso ao banco).
/// </summary>
public static class ValidacaoEnderecoAssociado
{
	/// <summary>
	/// Valores de T000ENDE.ID_EN_PCP.
	/// </summary>
	public const string Principal = "S";
	public const string NaoPrincipal = "N";

	/// <summary>
	/// Quantidade máxima de endereços por pessoa (SQ_EN_PES é numeric(2,0)).
	/// </summary>
	public const int MaximoEnderecos = 99;

	public static string? Validar(EnderecoAssociadoDados? dados)
	{
		if (dados is null)
		{
			return "Informe os dados do endereço.";
		}

		if (dados.CdTipo is null)
		{
			return "O tipo de endereço (cdTipo) é obrigatório.";
		}

		if (dados.CdTipo < 1 || dados.CdTipo > 99 || dados.CdTipo % 1 != 0)
		{
			return "O campo cdTipo deve ser um número inteiro entre 1 e 99.";
		}

		if (string.IsNullOrWhiteSpace(dados.Logradouro))
		{
			return "O logradouro é obrigatório.";
		}

		if (dados.Logradouro.Trim().Length > 50)
		{
			return "O campo logradouro aceita no máximo 50 caracteres.";
		}

		if (dados.Numero is not null && (dados.Numero < 0 || dados.Numero > 9_999_999_999m || dados.Numero % 1 != 0))
		{
			return "O campo numero deve ser um número inteiro de até 10 dígitos.";
		}

		if (dados.Complemento is not null && dados.Complemento.Trim().Length > 100)
		{
			return "O campo complemento aceita no máximo 100 caracteres.";
		}

		if (dados.Bairro is not null && dados.Bairro.Trim().Length > 30)
		{
			return "O campo bairro aceita no máximo 30 caracteres.";
		}

		if (!string.IsNullOrWhiteSpace(dados.Cep))
		{
			var digitos = ValidacaoTabelasApoio.SomenteDigitos(dados.Cep)!;
			var invalidos = dados.Cep.Count(ch => !char.IsAsciiDigit(ch) && ch is not ('-' or '.' or ' '));

			if (digitos.Length != 8 || invalidos > 0)
			{
				return "O campo cep deve ter 8 números.";
			}
		}

		if (dados.CdMunicipio is null)
		{
			return "O município (cdMunicipio) é obrigatório.";
		}

		if (dados.CdMunicipio < 1 || dados.CdMunicipio > 9_999_999 || dados.CdMunicipio % 1 != 0)
		{
			return "O campo cdMunicipio deve ser um número inteiro de até 7 dígitos.";
		}

		return null;
	}
}
