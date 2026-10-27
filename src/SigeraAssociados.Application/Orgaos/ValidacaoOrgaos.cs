using SigeraAssociados.Application.Cadastros;

namespace SigeraAssociados.Application.Orgaos;

/// <summary>
/// Regras de formato de órgão, lotação, situação do vínculo e vínculo
/// (sem acesso ao banco). Existência das referências é conferida na gravação.
/// </summary>
public static class ValidacaoOrgaos
{
	public const int TamanhoSituacaoVinculo = 35;

	public static string? Orgao(OrgaoDados? dados, bool inclusao)
	{
		if (dados is null)
		{
			return "Informe os dados do órgão.";
		}

		var erro = (inclusao ? Inteiro(dados.CdOrg, 999_999, "cdOrg") : null)
			?? Obrigatorio(dados.Nome, "nome")
			?? Tamanho(dados.Nome, 50, "nome")
			?? Tamanho(dados.Sigla, 100, "sigla")
			?? Tamanho(dados.Numero, 7, "numero")
			?? Inteiro(dados.CdGrupo, 999, "cdGrupo")
			?? Inteiro(dados.SqSubOrgao, 999, "sqSubOrgao")
			?? Uf(dados.Uf)
			?? Inteiro(dados.CdFil, 99, "cdFil")
			?? SituacaoDoOrgao(dados.CdSituacao)
			?? Endereco(dados.Endereco, "endereco")
			?? Contato(dados.ContatoAdministrativo, "contatoAdministrativo")
			?? Contato(dados.ContatoFinanceiro, "contatoFinanceiro");

		if (erro is not null || dados.EmpresaProcessadora is not { } ep)
		{
			return erro;
		}

		return Tamanho(ep.Nome, 200, "empresaProcessadora.nome")
			?? Tamanho(ep.Contato, 100, "empresaProcessadora.contato")
			?? Telefone(ep.Telefone, "empresaProcessadora.telefone")
			?? Email(ep.Email, "empresaProcessadora.email")
			?? Endereco(ep.Endereco, "empresaProcessadora.endereco");
	}

	public static string? Lotacao(LotacaoDados? dados, bool inclusao)
	{
		if (dados is null)
		{
			return "Informe os dados da lotação.";
		}

		return (inclusao ? Inteiro(dados.CdLot, 999_999, "cdLot") : null)
			?? Obrigatorio(dados.Descricao, "descricao")
			?? Tamanho(dados.Descricao, 50, "descricao")
			?? SituacaoAtivoInativo.Lotacao.Validar(dados.CdSituacao, "cdSituacao");
	}

	public static string? Grupo(GrupoDados? dados, bool inclusao)
	{
		if (dados is null)
		{
			return "Informe os dados do grupo.";
		}

		return (inclusao ? Inteiro(dados.CdGrupo, 999, "cdGrupo") : null)
			?? Obrigatorio(dados.Descricao, "descricao")
			?? Tamanho(dados.Descricao, 30, "descricao")
			?? (RegrasGrupoOrgao.TipoValido(dados.Tipo)
				? null
				: "O campo tipo é obrigatório: F (DIA FIXO) ou U (DIA ÚTIL).")
			?? (RegrasGrupoOrgao.OrdemValida(dados.Ordem)
				? null
				: "O campo ordem é obrigatório: A (ASCENDENTE) ou D (DESCENDENTE).")
			?? (dados.NumeroDias is not decimal dias
					|| dias < RegrasGrupoOrgao.NumeroDiasMinimo
					|| dias > RegrasGrupoOrgao.NumeroDiasMaximo
					|| dias % 1 != 0
				? "O campo numeroDias é obrigatório e deve ser um número inteiro de 1 a 31."
				: null);
	}

	public static string? SubOrgao(SubOrgaoDados? dados)
	{
		if (dados is null)
		{
			return "Informe os dados do subórgão.";
		}

		return Obrigatorio(dados.Nome, "nome") ?? Tamanho(dados.Nome, 100, "nome");
	}

	public static string? Cargo(CargoDados? dados, bool inclusao)
	{
		if (dados is null)
		{
			return "Informe os dados do cargo.";
		}

		return (inclusao ? Inteiro(dados.CdCargo, 9_999, "cdCargo") : null)
			?? Obrigatorio(dados.Descricao, "descricao")
			?? Tamanho(dados.Descricao, 30, "descricao")
			?? SituacaoAtivoInativo.Cargo.Validar(dados.CdSituacao, "cdSituacao");
	}

	public static string? Vinculo(VinculoOrgaoDados? dados)
	{
		if (dados is null)
		{
			return "Informe os dados do vínculo.";
		}

		if (dados.CdOrg is null)
		{
			return "O órgão (cdOrg) é obrigatório.";
		}

		return Inteiro(dados.CdOrg, 999_999, "cdOrg")
			?? Inteiro(dados.CdLotacao, 999_999, "cdLotacao")
			?? Inteiro(dados.CdSituacao, 99, "cdSituacao")
			?? Inteiro(dados.CdCargo, 9_999, "cdCargo")
			?? Tamanho(dados.Matricula, 20, "matricula")
			?? Tamanho(dados.DigitoMatricula, 5, "digitoMatricula")
			?? Tamanho(dados.MatriculaInstituidor, 20, "matriculaInstituidor")
			?? Valor(dados.Salario, "salario")
			?? Valor(dados.SalarioLiquido, "salarioLiquido");
	}

	private static string? Endereco(EnderecoDados? endereco, string campo)
	{
		if (endereco is null)
		{
			return null;
		}

		return Tamanho(endereco.Logradouro, 100, campo + ".logradouro")
			?? Inteiro(endereco.Numero, 99_999, campo + ".numero")
			?? Tamanho(endereco.Complemento, 50, campo + ".complemento")
			?? Tamanho(endereco.Bairro, 50, campo + ".bairro")
			?? Cep(endereco.Cep, campo + ".cep")
			?? Inteiro(endereco.CdMunicipio, 9_999_999, campo + ".cdMunicipio");
	}

	private static string? Contato(ContatoDados? contato, string campo)
	{
		if (contato is null)
		{
			return null;
		}

		return Tamanho(contato.Nome, 100, campo + ".nome")
			?? Telefone(contato.Telefone, campo + ".telefone")
			?? Email(contato.Email, campo + ".email");
	}

	private static string? Obrigatorio(string? valor, string campo) =>
		string.IsNullOrWhiteSpace(valor) ? $"O campo {campo} é obrigatório." : null;

	private static string? Tamanho(string? valor, int maximo, string campo) =>
		valor is not null && valor.Trim().Length > maximo
			? $"O campo {campo} aceita no máximo {maximo} caracteres."
			: null;

	private static string? Inteiro(decimal? valor, decimal maximo, string campo) =>
		valor is not null && (valor < 1 || valor > maximo || valor % 1 != 0)
			? $"O campo {campo} deve ser um número inteiro entre 1 e {maximo:0}."
			: null;

	private static string? SituacaoDoOrgao(decimal? valor) =>
		SituacaoOrgao.Valida(valor) ? null : "O campo cdSituacao do órgão deve ser 1 (ATIVO) ou 0 (INATIVO).";


	private static string? Valor(decimal? valor, string campo) =>
		valor is not null && (valor < 0 || valor > 9_999_999.99m || decimal.Round(valor.Value, 2) != valor)
			? $"O campo {campo} deve estar entre 0 e 9999999,99, com até 2 casas decimais."
			: null;

	private static string? Email(string? valor, string campo) =>
		Tamanho(valor, 100, campo)
		?? (!string.IsNullOrWhiteSpace(valor) && !valor.Contains('@') ? $"O campo {campo} é inválido." : null);

	private static string? Telefone(string? valor, string campo)
	{
		if (string.IsNullOrWhiteSpace(valor))
		{
			return null;
		}

		var digitos = ValidacaoTabelasApoio.SomenteDigitos(valor)!;
		var invalidos = valor.Count(ch => !char.IsAsciiDigit(ch) && ch is not (' ' or '-' or '(' or ')' or '+'));

		return digitos.Length is < 8 or > 15 || invalidos > 0
			? $"O campo {campo} deve ter de 8 a 15 números."
			: null;
	}

	private static string? Cep(string? valor, string campo)
	{
		if (string.IsNullOrWhiteSpace(valor))
		{
			return null;
		}

		var digitos = ValidacaoTabelasApoio.SomenteDigitos(valor)!;
		var invalidos = valor.Count(ch => !char.IsAsciiDigit(ch) && ch is not ('-' or '.' or ' '));

		return digitos.Length != 8 || invalidos > 0 ? $"O campo {campo} deve ter 8 números." : null;
	}

	private static string? Uf(string? uf)
	{
		if (string.IsNullOrWhiteSpace(uf))
		{
			return null;
		}

		var valor = uf.Trim();

		return valor.Length == 2 && valor.All(char.IsAsciiLetter) ? null : "A UF deve ter 2 letras (ex.: CE).";
	}
}
