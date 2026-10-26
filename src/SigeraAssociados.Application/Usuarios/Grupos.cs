namespace SigeraAssociados.Application.Usuarios;

/// <summary>
/// Nomes oficiais dos grupos (funções do Identity) e as combinações
/// usadas na autorização dos endpoints.
/// Único lugar onde esses nomes devem ser escritos.
/// </summary>
public static class Grupos
{
	public const string Administradores = "Administradores";
	public const string Gestores = "Gestores";
	public const string CoordenadoraAdmOperacional = "Coordenadora Adm. & Operacional";
	public const string Corretores = "Corretores";
	public const string Financeiro = "Financeiro";
	public const string Associados = "Associados";
	public const string Atendimento = "Atendimento";

	/// <summary>
	/// Função antiga mantida por compatibilidade. Equivale a Administradores.
	/// </summary>
	public const string AdministradorLegado = "Administrador";

	/// <summary>
	/// Perfis com acesso às rotas exclusivas de administração
	/// (acessos de usuários e diagnóstico). Uso: [Authorize(Roles = ...)].
	/// </summary>
	public const string PerfisAdministracao =
		AdministradorLegado + "," + Administradores;

	/// <summary>
	/// Perfis que podem criar e gerenciar usuários.
	/// </summary>
	public const string PerfisGestaoUsuarios =
		AdministradorLegado + "," + Administradores + "," + Gestores;

	/// <summary>
	/// Perfis que podem excluir associados e marcá-los como falecidos
	/// (a situação falecido encerra os contratos ativos pela trigger TR_CLIE_UPD).
	/// </summary>
	public const string PerfisExclusaoAssociados =
		AdministradorLegado + "," + Administradores + "," + Gestores;

	/// <summary>
	/// Os mesmos perfis de PerfisExclusaoAssociados, em lista.
	/// </summary>
	public static readonly IReadOnlyList<string> PodemMarcarFalecido =
	[
		AdministradorLegado,
		Administradores,
		Gestores
	];

	/// <summary>
	/// Os sete grupos funcionais oficiais.
	/// </summary>
	public static readonly IReadOnlyList<string> Funcionais =
	[
		Administradores,
		Gestores,
		CoordenadoraAdmOperacional,
		Corretores,
		Financeiro,
		Associados,
		Atendimento
	];

	/// <summary>
	/// Grupos com acesso irrestrito a todos os sistemas e dados.
	/// </summary>
	public static readonly IReadOnlyList<string> AcessoIrrestrito =
	[
		Administradores,
		AdministradorLegado
	];

	/// <summary>
	/// Devolve o nome oficial do grupo funcional (ignorando maiúsculas)
	/// ou null quando o nome não corresponde a nenhum grupo.
	/// </summary>
	public static string? Normalizar(string? nome)
	{
		if (string.IsNullOrWhiteSpace(nome))
		{
			return null;
		}

		var informado = nome.Trim();

		return Funcionais.FirstOrDefault(grupo =>
			string.Equals(grupo, informado, StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Indica se a lista de funções contém um grupo administrativo.
	/// </summary>
	public static bool ContemAdministrador(IEnumerable<string> funcoes)
	{
		return funcoes.Any(funcao => AcessoIrrestrito.Contains(
			funcao,
			StringComparer.OrdinalIgnoreCase));
	}
}
