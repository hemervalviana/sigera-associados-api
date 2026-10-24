using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Api.DTOs.Acessos;

/// <summary>
/// Dados para conceder um escopo de acesso a um usuário.
///
/// Exemplos:
/// - Gestor de todo o SONASP: { "sistema": 2 }
/// - Empresa 1 inteira do GSP: { "sistema": 4, "cdEmp": 1 }
/// - Filial 3 da empresa 1: { "sistema": 4, "cdEmp": 1, "cdFil": 3 }
/// - Carteira do corretor 1234 na empresa 1:
///   { "sistema": 4, "cdEmp": 1, "sqPesCorretor": 1234 }
/// </summary>
public sealed class ConcederAcessoRequest
{
	/// <summary>
	/// Sistema: 1 PROASP, 2 SONASP, 3 ABSP, 4 GSP.
	/// </summary>
	[Range(1, 4, ErrorMessage = "O sistema deve ser 1, 2, 3 ou 4.")]
	public int Sistema { get; init; }

	/// <summary>
	/// Empresa. Omitir para liberar todas as empresas do sistema.
	/// </summary>
	[Range(1, 99, ErrorMessage = "A empresa deve estar entre 1 e 99.")]
	public decimal? CdEmp { get; init; }

	/// <summary>
	/// Filial. Omitir para liberar todas as filiais da empresa.
	/// </summary>
	[Range(1, 99, ErrorMessage = "A filial deve estar entre 1 e 99.")]
	public decimal? CdFil { get; init; }

	/// <summary>
	/// Pessoa corretor (SQ_PES). Quando informado, o usuário vê somente
	/// os associados com contrato desse corretor.
	/// </summary>
	[Range(1, 9999999, ErrorMessage = "O corretor deve estar entre 1 e 9999999.")]
	public decimal? SqPesCorretor { get; init; }
}
