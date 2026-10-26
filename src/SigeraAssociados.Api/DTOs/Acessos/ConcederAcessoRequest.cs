using System.ComponentModel.DataAnnotations;

namespace SigeraAssociados.Api.DTOs.Acessos;

/// <summary>
/// Dados para conceder um escopo de acesso a um usuário.
///
/// Cada sistema é uma base com uma única empresa (CD_EMP), por isso a
/// empresa não é informada: a API usa a empresa da base escolhida.
///
/// Exemplos:
/// - Todos os sistemas inteiros: { "sistema": 0 }
/// - SONASP inteiro: { "sistema": 2 }
/// - Filial 3 do GSP: { "sistema": 4, "cdFil": 3 }
/// - Carteira do corretor 1234 no GSP: { "sistema": 4, "sqPesCorretor": 1234 }
/// </summary>
public sealed class ConcederAcessoRequest
{
	/// <summary>
	/// Sistema: 0 Todos, 1 PROASP, 2 SONASP, 3 ABSP, 4 GSP.
	/// Todos (0) libera os sistemas inteiros e não aceita filial nem corretor.
	/// </summary>
	/// <example>2</example>
	[Range(0, 4, ErrorMessage = "O sistema deve ser 0 (todos), 1, 2, 3 ou 4.")]
	public int Sistema { get; init; }

	/// <summary>
	/// Filial. Omitir para liberar todas as filiais do sistema.
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
