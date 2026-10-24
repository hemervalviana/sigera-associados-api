namespace SigeraAssociados.Application.Interfaces.Acesso;

/// <summary>
/// Dados do usuário autenticado na requisição atual.
/// </summary>
public interface IUsuarioAtual
{
	/// <summary>
	/// Identificador do usuário no Identity. Nulo quando anônimo.
	/// </summary>
	string? UsuarioId { get; }

	/// <summary>
	/// Indica se o usuário pertence a um grupo com acesso irrestrito
	/// a todos os sistemas e dados (Administradores).
	/// </summary>
	bool PossuiAcessoIrrestrito { get; }
}
