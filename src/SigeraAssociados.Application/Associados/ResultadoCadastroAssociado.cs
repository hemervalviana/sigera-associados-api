namespace SigeraAssociados.Application.Associados;

/// <summary>
/// Situação final de uma inclusão, alteração ou exclusão de associado.
/// </summary>
public enum StatusCadastroAssociado
{
	Sucesso,

	/// <summary>
	/// Não existe, foi excluído ou está fora dos acessos do usuário.
	/// </summary>
	NaoEncontrado,

	Invalido,

	/// <summary>
	/// O usuário não pode fazer a operação (filial fora dos acessos,
	/// ou marcar falecido sem ser Administrador ou Gestor).
	/// </summary>
	Proibido,

	/// <summary>
	/// CPF já cadastrado em outra pessoa.
	/// </summary>
	Conflito,

	Falha
}

/// <param name="ContratosEncerrados">
/// Ao marcar falecido: quantos contratos ativos (situação 2 ou 5)
/// a trigger TR_CLIE_UPD passou para 7.
/// </param>
public sealed record ResultadoCadastroAssociado(
	StatusCadastroAssociado Status,
	string? Mensagem = null,
	AssociadoCadastroDto? Associado = null,
	int? ContratosEncerrados = null);

/// <summary>
/// Quem está gravando: login (vai para NM_USU e para a exclusão)
/// e se pode marcar o associado como falecido.
/// </summary>
public sealed record ExecutorCadastro(string Login, bool PodeMarcarFalecido);
