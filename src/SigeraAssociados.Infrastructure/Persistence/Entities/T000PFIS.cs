using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeamento parcial (somente leitura) da tabela legada dbo.T000PFIS,
/// cadastro complementar da pessoa física.
/// Contém apenas a chave, usada para validar o cadastro do associado:
/// pessoa com TP_PES = 'F' precisa existir nesta tabela.
/// Não cria nem modifica a tabela existente.
/// </summary>
[Table("T000PFIS", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES_FIS))]
public sealed class T000PFIS
{
	/// <summary>
	/// Código da empresa. Referencia T000PESS.CD_EMP.
	/// </summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>
	/// Pessoa física. Referencia T000PESS.SQ_PES.
	/// </summary>
	[Column("SQ_PES_FIS", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES_FIS { get; set; }
}
