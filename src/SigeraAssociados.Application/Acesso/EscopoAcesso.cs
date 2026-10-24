namespace SigeraAssociados.Application.Acesso;

/// <summary>
/// Um escopo de dados liberado ao usuário dentro do sistema selecionado.
/// </summary>
/// <param name="CdEmp">Empresa. Nulo = todas as empresas.</param>
/// <param name="CdFil">Filial. Nulo = todas as filiais da empresa.</param>
/// <param name="SqPesCorretor">
/// Corretor. Quando preenchido, restringe aos associados
/// com contrato desse corretor.
/// </param>
public sealed record EscopoAcesso(
	decimal? CdEmp,
	decimal? CdFil,
	decimal? SqPesCorretor);
