using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Financeiro;

namespace SigeraAssociados.Application.Corretores;

/// <summary>
/// Regras de formato do cadastro de corretor (sem acesso ao banco).
/// Existência de filial, município, tratamento, representante, coordenador
/// e agente, e unicidade de CPF/CNPJ, são conferidas na gravação.
/// </summary>
public static class ValidacaoCadastroCorretor
{
	public const string PessoaFisica = "F";
	public const string PessoaJuridica = "J";

	public const int TamanhoSenha = 20;

	// Faixa do smalldatetime das datas de T000PFIS.
	private static readonly DateTime DataMinima = new(1900, 1, 1);
	private static readonly DateTime DataMaxima = new(2079, 6, 6);

	/// <summary>F ou J (sem diferenciar maiúsculas); null quando inválido.</summary>
	public static string? NormalizarTipo(string? tipo) =>
		tipo?.Trim().ToUpperInvariant() is PessoaFisica or PessoaJuridica ? tipo.Trim().ToUpperInvariant() : null;

	public static string? Validar(CadastroCorretorDados? dados)
	{
		if (dados is null)
		{
			return "Informe os dados do corretor.";
		}

		var tipo = NormalizarTipo(dados.TipoPessoa);

		if (tipo is null)
		{
			return "O campo tipoPessoa é obrigatório: F (física) ou J (jurídica).";
		}

		var erro = RegrasCampos.Obrigatorio(dados.Nome, "nome")
			?? RegrasCampos.Tamanho(dados.Nome, 80, "nome")
			?? RegrasCampos.Tamanho(dados.NomeCurto, 20, "nomeCurto")
			?? RegrasCampos.Tamanho(dados.Email, 50, "email")
			?? RegrasCampos.Tamanho(dados.Url, 50, "url")
			?? RegrasCampos.Tamanho(dados.Observacao, 255, "observacao");

		if (erro is not null)
		{
			return erro;
		}

		if (!string.IsNullOrWhiteSpace(dados.Email) && !dados.Email.Contains('@'))
		{
			return "O e-mail é inválido.";
		}

		// Física ou jurídica, nunca as duas, e sempre uma delas.
		if (tipo == PessoaFisica)
		{
			if (dados.PessoaJuridica is not null)
			{
				return "Corretor pessoa física (tipoPessoa F) não leva pessoaJuridica: envie só pessoaFisica.";
			}

			if (dados.PessoaFisica is null)
			{
				return "Informe os dados de pessoa física (pessoaFisica).";
			}

			erro = Fisica(dados.PessoaFisica);
		}
		else
		{
			if (dados.PessoaFisica is not null)
			{
				return "Corretor pessoa jurídica (tipoPessoa J) não leva pessoaFisica: envie só pessoaJuridica.";
			}

			if (dados.PessoaJuridica is null)
			{
				return "Informe os dados de pessoa jurídica (pessoaJuridica).";
			}

			erro = Juridica(dados.PessoaJuridica);
		}

		return erro ?? Corretor(dados.Corretor);
	}

	private static string? Fisica(PessoaFisicaDados pf)
	{
		if (string.IsNullOrWhiteSpace(pf.Cpf))
		{
			return "O CPF é obrigatório.";
		}

		if (!DocumentoCpf.Valido(DocumentoCpf.SomenteDigitos(pf.Cpf)))
		{
			return "CPF inválido. Informe os 11 números do CPF, com ou sem pontuação.";
		}

		if (pf.DataNascimento is DateTime nascimento && (nascimento.Date > DateTime.Today || nascimento < DataMinima))
		{
			return "A data de nascimento é inválida.";
		}

		if (!string.IsNullOrWhiteSpace(pf.Sexo) && pf.Sexo.Trim().ToUpperInvariant() is not ("M" or "F"))
		{
			return "O sexo deve ser M ou F.";
		}

		return RegrasCampos.Tamanho(pf.EstadoCivil, 1, "estadoCivil")
			?? RegrasCampos.Tamanho(pf.Rg, 40, "rg")
			?? RegrasCampos.Tamanho(pf.OrgaoExpedidor, 10, "orgaoExpedidor")
			?? RegrasCampos.Tamanho(pf.UfOrgao, 8, "ufOrgao")
			?? RegrasCampos.Tamanho(pf.NomePai, 50, "nomePai")
			?? RegrasCampos.Tamanho(pf.NomeMae, 50, "nomeMae")
			?? RegrasCampos.Tamanho(pf.NomeConjuge, 50, "nomeConjuge")
			?? RegrasCampos.Inteiro(pf.CdMunicipio, 1, 9_999_999, "cdMunicipio")
			?? RegrasCampos.Inteiro(pf.CdTratamento, 1, 99, "cdTratamento")
			?? Data(pf.DataNascimentoConjuge, "dataNascimentoConjuge")
			?? Data(pf.DataCasamento, "dataCasamento");
	}

	private static string? Juridica(PessoaJuridicaDados pj)
	{
		if (string.IsNullOrWhiteSpace(pj.Cnpj))
		{
			return "O CNPJ é obrigatório.";
		}

		if (!DocumentoCnpj.Valido(DocumentoCnpj.SomenteDigitos(pj.Cnpj)))
		{
			return "CNPJ inválido. Informe os 14 números do CNPJ, com ou sem pontuação.";
		}

		return RegrasCampos.Inteiro(pj.InscricaoEstadual, 0, 999_999_999, "inscricaoEstadual")
			?? RegrasCampos.Tamanho(pj.Contato, 50, "contato")
			?? RegrasCampos.Inteiro(pj.CdCargo, 1, 9_999, "cdCargo");
	}

	private static string? Corretor(DadosCorretor? c)
	{
		if (c is null)
		{
			return null;
		}

		if (c.Classe is not null && c.Classe.Trim().Length > 1)
		{
			return "O campo classe aceita 1 caractere.";
		}

		return RegrasCampos.Inteiro(c.CdFil, 1, 99, "cdFil")
			?? RegrasCampos.Inteiro(c.CdMunicipio, 1, 9_999_999, "cdMunicipio")
			?? RegrasCampos.Inteiro(c.CdSituacao, 0, 9, "cdSituacao")
			?? (c.Tipo is < 0 ? "O campo tipo não pode ser negativo." : null)
			?? RegrasCampos.Inteiro(c.InscricaoSusep, 0, 999_999_999_999_999m, "inscricaoSusep")
			?? Percentual(c.ComissaoAdesao, "comissaoAdesao")
			?? Percentual(c.ComissaoCarencia, "comissaoCarencia")
			?? Percentual(c.ComissaoPlano, "comissaoPlano")
			?? RegrasCampos.Inteiro(c.SqPesRepresentante, 1, 9_999_999, "sqPesRepresentante")
			?? Senha(c.SenhaRepresentante, "senhaRepresentante")
			?? RegrasCampos.Inteiro(c.SqCoordenador, 1, 9_999_999, "sqCoordenador")
			?? RegrasCampos.Inteiro(c.SqAgente, 1, 99_999_999, "sqAgente")
			?? RegrasCampos.Inteiro(c.SqGved, 1, 99_999_999, "sqGved");
	}

	private static string? Percentual(decimal? valor, string campo) =>
		valor is decimal v && (v < 0 || v > 100 || decimal.Round(v, 2) != v)
			? $"O campo {campo} deve ser um percentual de 0 a 100, com até 2 casas."
			: null;

	private static string? Data(DateTime? valor, string campo) =>
		valor is DateTime d && (d.Date > DateTime.Today || d < DataMinima || d > DataMaxima)
			? $"O campo {campo} é inválido."
			: null;

	/// <summary>
	/// Senha opcional: até 20 caracteres, sem caracteres de controle e só com
	/// o que o varchar do legado guarda sem trocar (Latin-1). Não é aparada.
	/// </summary>
	private static string? Senha(string? valor, string campo)
	{
		if (string.IsNullOrEmpty(valor))
		{
			return null;
		}

		if (valor.Length > TamanhoSenha)
		{
			return $"O campo {campo} aceita no máximo {TamanhoSenha} caracteres.";
		}

		return valor.All(ch => ch is >= ' ' and <= '~' or >= ' ' and <= 'ÿ')
			? null
			: $"O campo {campo} tem caracteres não aceitos (use letras, números e símbolos comuns).";
	}
}
