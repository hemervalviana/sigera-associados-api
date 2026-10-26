namespace SigeraAssociados.Application.Acesso;

/// <summary>
/// Resultado de uma operação de concessão ou revogação de acesso.
/// </summary>
public enum StatusGestaoAcesso
{
	Sucesso,
	UsuarioNaoEncontrado,
	AcessoNaoEncontrado,
	Duplicado,
	Invalido
}

public sealed record ResultadoGestaoAcesso(
	StatusGestaoAcesso Status,
	string? Mensagem = null,
	UsuarioAcessoDto? Acesso = null,
	IReadOnlyList<UsuarioAcessoDto>? Acessos = null);
