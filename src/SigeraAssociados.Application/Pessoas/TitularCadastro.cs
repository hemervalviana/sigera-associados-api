namespace SigeraAssociados.Application.Pessoas;

/// <summary>
/// De quem são os endereços, telefones, contas, referências e chaves Pix.
/// As tabelas são por pessoa (SQ_PES); o titular define a conferência de
/// acesso: associado (escopo de filial e carteira) ou corretor (não
/// excluído e coerente com o tipo de pessoa).
/// </summary>
public enum TitularCadastro
{
	Associado,
	Corretor
}
