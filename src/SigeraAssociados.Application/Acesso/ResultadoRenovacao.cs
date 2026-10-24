namespace SigeraAssociados.Application.Acesso;

/// <summary>
/// Token de renovação recém-emitido (o valor só existe nesta resposta).
/// </summary>
public sealed record TokenRenovacaoEmitido(string Token, DateTimeOffset ExpiraEm);

public enum StatusRenovacao
{
	Sucesso,

	/// <summary>Token desconhecido ou já revogado por logout.</summary>
	Invalido,

	Expirado,

	/// <summary>
	/// Token já trocado foi usado de novo (possível roubo):
	/// todas as sessões do usuário foram revogadas.
	/// </summary>
	Reutilizado,

	/// <summary>Usuário excluído, bloqueado ou sem e-mail confirmado.</summary>
	UsuarioIndisponivel
}

public sealed record ResultadoRenovacao(
	StatusRenovacao Status,
	string? UsuarioId = null,
	TokenRenovacaoEmitido? NovoToken = null);
