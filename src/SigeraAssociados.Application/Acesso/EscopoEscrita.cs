namespace SigeraAssociados.Application.Acesso;

/// <summary>
/// Aplica o escopo de acesso às gravações de associados, com as mesmas
/// regras da consulta (AssociadosRepository.AplicarEscopo):
/// - sistema inteiro: tudo;
/// - filial: associados dessa filial (associado sem filial não entra);
/// - carteira do corretor: associados com contrato do corretor ou
///   cadastrados por ele (T000CLIE.SQ_PES_CRO_CAD), na filial se informada.
/// </summary>
public static class EscopoEscrita
{
	/// <summary>
	/// O usuário pode ver, alterar ou excluir um associado da filial
	/// informada, ligado aos corretores informados (contratos + cadastro)?
	/// </summary>
	public static bool PermiteAssociado(
		EscopoConsulta escopo,
		decimal? cdFil,
		IReadOnlyCollection<decimal> corretoresDoAssociado)
	{
		ArgumentNullException.ThrowIfNull(escopo);
		ArgumentNullException.ThrowIfNull(corretoresDoAssociado);

		if (escopo.AcessoTotal)
		{
			return true;
		}

		return escopo.Escopos.Any(e =>
			(e.CdFil is null || e.CdFil == cdFil)
			&& (e.SqPesCorretor is null || corretoresDoAssociado.Contains(e.SqPesCorretor.Value)));
	}

	/// <summary>
	/// O usuário pode incluir um associado nessa filial? Para quem só tem
	/// acesso pela carteira de corretor, devolve o corretor a vincular.
	/// </summary>
	public static AvaliacaoInclusao AvaliarInclusao(
		EscopoConsulta escopo,
		decimal? cdFil,
		decimal? corretorPedido)
	{
		ArgumentNullException.ThrowIfNull(escopo);

		if (escopo.AcessoTotal)
		{
			return AvaliacaoInclusao.Permitida(corretorPedido);
		}

		var itens = escopo.Escopos
			.Where(e => e.CdFil is null || e.CdFil == cdFil)
			.ToList();

		// Acesso à filial (ou ao sistema) sem corretor: inclui livremente.
		if (itens.Any(e => e.SqPesCorretor is null))
		{
			return AvaliacaoInclusao.Permitida(corretorPedido);
		}

		var corretores = itens
			.Select(e => e.SqPesCorretor!.Value)
			.Distinct()
			.ToList();

		if (corretores.Count == 0)
		{
			return AvaliacaoInclusao.Recusada(
				$"Você não tem acesso para cadastrar associados na filial {cdFil:0}.");
		}

		if (corretorPedido is decimal pedido)
		{
			return corretores.Contains(pedido)
				? AvaliacaoInclusao.Permitida(pedido)
				: AvaliacaoInclusao.Recusada(
					$"O corretor {pedido:0} não está entre os seus acessos na filial {cdFil:0}.");
		}

		return corretores.Count == 1
			? AvaliacaoInclusao.Permitida(corretores[0])
			: AvaliacaoInclusao.FaltaCorretor(
				"Você tem mais de um corretor liberado nesta filial. " +
				"Informe sqPesCorretor: " + string.Join(", ", corretores.Select(c => c.ToString("0"))) + ".");
	}
}

/// <param name="Permitido">Pode incluir.</param>
/// <param name="Corretor">Corretor a vincular no cadastro (SQ_PES_CRO_CAD).</param>
/// <param name="FaltaEscolherCorretor">Recusada só porque falta escolher o corretor (400, não 403).</param>
/// <param name="Mensagem">Motivo da recusa.</param>
public sealed record AvaliacaoInclusao(
	bool Permitido,
	decimal? Corretor,
	bool FaltaEscolherCorretor,
	string? Mensagem)
{
	public static AvaliacaoInclusao Permitida(decimal? corretor) => new(true, corretor, false, null);

	public static AvaliacaoInclusao Recusada(string mensagem) => new(false, null, false, mensagem);

	public static AvaliacaoInclusao FaltaCorretor(string mensagem) => new(false, null, true, mensagem);
}
