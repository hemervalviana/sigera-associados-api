using SigeraAssociados.Application.Orgaos;

namespace SigeraAssociados.Application.Cadastros;

/// <summary>
/// Dados de inclusão e alteração de uma filial (T000PARA).
/// Na alteração, o código vem da rota e CdFil é ignorado.
/// </summary>
public sealed class FilialDados
{
	/// <summary>
	/// Código da filial (cd_fil, 1 a 99). Na inclusão, omitir para usar MAX + 1.
	/// </summary>
	public decimal? CdFil { get; init; }

	/// <summary>
	/// Nome da filial (NM_EMP, até 80). Obrigatório.
	/// </summary>
	public string? Nome { get; init; }

	/// <summary>NC_EMP, até 20.</summary>
	public string? NomeCurto { get; init; }

	/// <summary>NM_LOG_EMP, até 50.</summary>
	public string? Logradouro { get; init; }

	/// <summary>NR_LOG_EMP, até 99999.</summary>
	public decimal? Numero { get; init; }

	/// <summary>CP_EN_EMP, até 30.</summary>
	public string? Complemento { get; init; }

	/// <summary>NM_BAI_EMP, até 30.</summary>
	public string? Bairro { get; init; }

	/// <summary>NR_CEP_EMP: 8 números, com ou sem traço.</summary>
	public string? Cep { get; init; }

	/// <summary>DE_MUN_EMP, até 55.</summary>
	public string? Municipio { get; init; }

	/// <summary>SG_UF_EMP: 2 letras.</summary>
	public string? Uf { get; init; }

	/// <summary>NR_DDD_FN, 1 a 999.</summary>
	public decimal? Ddd { get; init; }

	/// <summary>NR_FN, até 12.</summary>
	public string? Telefone { get; init; }

	/// <summary>NR_DDD_FAX, 1 a 999.</summary>
	public decimal? DddFax { get; init; }

	/// <summary>NR_FAX, até 12.</summary>
	public string? Fax { get; init; }

	/// <summary>NR_CPJ: CNPJ com ou sem pontuação.</summary>
	public string? Cnpj { get; init; }
}

/// <summary>
/// Dados de inclusão e alteração de uma situação do associado (T000SCLI)
/// ou de um tratamento (T000TRTA). Na alteração, o código vem da rota.
/// </summary>
public sealed class ItemTabelaDados
{
	/// <summary>
	/// Código (1 a 99). Na inclusão, omitir para usar MAX + 1.
	/// </summary>
	public decimal? Codigo { get; init; }

	/// <summary>
	/// Descrição. Obrigatória (situação até 30, tratamento até 20).
	/// </summary>
	public string? Descricao { get; init; }
}

/// <summary>
/// Dados de inclusão e alteração de um município (T000MUNI).
/// Na alteração, o código vem da rota.
/// </summary>
public sealed class MunicipioDados
{
	/// <summary>
	/// Código do município (CD_MUN, até 7 dígitos, ex.: código do IBGE).
	/// Obrigatório na inclusão.
	/// </summary>
	public decimal? CdMun { get; init; }

	/// <summary>
	/// Nome (DE_MUN, até 400). Obrigatório.
	/// </summary>
	public string? Nome { get; init; }

	/// <summary>
	/// UF (SG_UF, 2 letras). Obrigatória.
	/// </summary>
	public string? Uf { get; init; }
}

/// <summary>
/// Banco (T000BANC). Na alteração, o código vem da rota.
/// </summary>
public sealed class BancoDados
{
	/// <summary>
	/// Código do banco (CD_BCO, 1 a 999, ex.: 1 Banco do Brasil, 104 Caixa).
	/// Obrigatório na inclusão (não é gerado).
	/// </summary>
	public decimal? CdBanco { get; init; }

	/// <summary>Nome (NM_BCO, até 35). Obrigatório.</summary>
	public string? Nome { get; init; }

	/// <summary>
	/// Situação do banco (CD_SIT_BCO): 1 = ATIVO, 0 = INATIVO.
	/// Omitida na inclusão: 1 (ATIVO).
	/// </summary>
	public decimal? CdSituacao { get; init; }

	/// <summary>ATIVO ou INATIVO. Só na resposta.</summary>
	public string? Situacao { get; init; }
}

/// <summary>
/// Situação final de uma inclusão, alteração ou exclusão em tabela de apoio.
/// </summary>
public enum StatusTabelaApoio
{
	Sucesso,
	NaoEncontrado,
	Invalido,

	/// <summary>
	/// Código já existe, ou o registro está em uso e não pode ser excluído.
	/// </summary>
	Conflito,

	Falha,

	/// <summary>Fora dos acessos do usuário para esta gravação (403).</summary>
	Proibido
}

public sealed record ResultadoTabelaApoio<T>(
	StatusTabelaApoio Status,
	string? Mensagem = null,
	T? Item = default);

/// <summary>
/// Regras de formato das tabelas de apoio (sem acesso ao banco).
/// </summary>
public static class ValidacaoTabelasApoio
{
	public const int TamanhoSituacao = 30;
	public const int TamanhoTratamento = 20;
	public const int TamanhoTipoEndereco = 20;
	public const int TamanhoTipoTelefone = 20;

	public static string? Filial(FilialDados? dados, bool inclusao)
	{
		if (dados is null)
		{
			return "Informe os dados da filial.";
		}

		return (inclusao ? Codigo(dados.CdFil, 99, "cdFil") : null)
			?? Obrigatorio(dados.Nome, "nome")
			?? Tamanho(dados.Nome, 80, "nome")
			?? Tamanho(dados.NomeCurto, 20, "nomeCurto")
			?? Tamanho(dados.Logradouro, 50, "logradouro")
			?? Codigo(dados.Numero, 99_999, "numero")
			?? Tamanho(dados.Complemento, 30, "complemento")
			?? Tamanho(dados.Bairro, 30, "bairro")
			?? Digitos(dados.Cep, 8, "cep")
			?? Tamanho(dados.Municipio, 55, "municipio")
			?? UfOpcional(dados.Uf)
			?? Codigo(dados.Ddd, 999, "ddd")
			?? Tamanho(dados.Telefone, 12, "telefone")
			?? Codigo(dados.DddFax, 999, "dddFax")
			?? Tamanho(dados.Fax, 12, "fax")
			?? CnpjOpcional(dados.Cnpj);
	}

	public static string? Item(ItemTabelaDados? dados, int tamanhoDescricao, bool inclusao)
	{
		if (dados is null)
		{
			return "Informe o código e a descrição.";
		}

		return (inclusao ? Codigo(dados.Codigo, 99, "codigo") : null)
			?? Obrigatorio(dados.Descricao, "descricao")
			?? Tamanho(dados.Descricao, tamanhoDescricao, "descricao");
	}

	public static string? Banco(BancoDados? dados, bool inclusao)
	{
		if (dados is null)
		{
			return "Informe os dados do banco.";
		}

		if (inclusao && dados.CdBanco is null)
		{
			return "O código do banco (cdBanco) é obrigatório.";
		}

		return (inclusao ? Codigo(dados.CdBanco, 999, "cdBanco") : null)
			?? Obrigatorio(dados.Nome, "nome")
			?? Tamanho(dados.Nome, 35, "nome")
			?? SituacaoAtivoInativo.Banco.Validar(dados.CdSituacao, "cdSituacao");
	}

	public static string? Municipio(MunicipioDados? dados, bool inclusao)
	{
		if (dados is null)
		{
			return "Informe os dados do município.";
		}

		if (inclusao && dados.CdMun is null)
		{
			return "O código do município (cdMun) é obrigatório.";
		}

		return (inclusao ? Codigo(dados.CdMun, 9_999_999, "cdMun") : null)
			?? Obrigatorio(dados.Nome, "nome")
			?? Tamanho(dados.Nome, 400, "nome")
			?? (string.IsNullOrWhiteSpace(dados.Uf) ? "A UF é obrigatória." : UfOpcional(dados.Uf));
	}

	/// <summary>
	/// Só os dígitos do texto; null quando vazio.
	/// </summary>
	public static string? SomenteDigitos(string? valor)
	{
		if (string.IsNullOrWhiteSpace(valor))
		{
			return null;
		}

		return new string(valor.Where(char.IsAsciiDigit).ToArray());
	}

	private static string? Obrigatorio(string? valor, string campo) =>
		string.IsNullOrWhiteSpace(valor) ? $"O campo {campo} é obrigatório." : null;

	private static string? Tamanho(string? valor, int maximo, string campo) =>
		valor is not null && valor.Trim().Length > maximo
			? $"O campo {campo} aceita no máximo {maximo} caracteres."
			: null;

	private static string? Codigo(decimal? valor, decimal maximo, string campo) =>
		valor is not null && (valor < 1 || valor > maximo || valor % 1 != 0)
			? $"O campo {campo} deve ser um número inteiro entre 1 e {maximo:0}."
			: null;

	private static string? Digitos(string? valor, int quantidade, string campo)
	{
		if (string.IsNullOrWhiteSpace(valor))
		{
			return null;
		}

		var digitos = SomenteDigitos(valor)!;
		var outros = valor.Count(ch => !char.IsAsciiDigit(ch) && ch is not ('.' or '-' or '/' or ' '));

		return digitos.Length != quantidade || outros > 0
			? $"O campo {campo} deve ter {quantidade} números."
			: null;
	}

	private static string? UfOpcional(string? uf)
	{
		if (string.IsNullOrWhiteSpace(uf))
		{
			return null;
		}

		var valor = uf.Trim();

		return valor.Length == 2 && valor.All(char.IsAsciiLetter)
			? null
			: "A UF deve ter 2 letras (ex.: CE).";
	}

	private static string? CnpjOpcional(string? cnpj)
	{
		if (string.IsNullOrWhiteSpace(cnpj))
		{
			return null;
		}

		if (Digitos(cnpj, 14, "cnpj") is string erro)
		{
			return erro;
		}

		var d = SomenteDigitos(cnpj)!.Select(ch => ch - '0').ToArray();

		if (d.Distinct().Count() == 1)
		{
			return "CNPJ inválido.";
		}

		int Digito(int tamanho)
		{
			int[] pesos = tamanho == 12
				? [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
				: [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

			var soma = 0;

			for (var i = 0; i < tamanho; i++)
			{
				soma += d[i] * pesos[i];
			}

			var resto = soma % 11;

			return resto < 2 ? 0 : 11 - resto;
		}

		return Digito(12) == d[12] && Digito(13) == d[13] ? null : "CNPJ inválido.";
	}
}
