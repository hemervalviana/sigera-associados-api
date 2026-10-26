using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000CARG: cargos.
/// Referenciada por T000RPOR.CD_CG (cargo do associado no órgão).
/// </summary>
[Table("T000CARG", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_CG))]
public sealed class T000CARG
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código do cargo. NOT NULL.</summary>
	[Column("CD_CG", TypeName = "numeric(4, 0)")]
	[Precision(4, 0)]
	public decimal CD_CG { get; set; }

	/// <summary>Descrição. NOT NULL.</summary>
	[Required]
	[MaxLength(30)]
	[Column("DE_CG", TypeName = "varchar(30)")]
	public string DE_CG { get; set; } = string.Empty;

	/// <summary>Situação do cargo (0 a 9).</summary>
	[Column("CD_SIT_CAG", TypeName = "numeric(1, 0)")]
	[Precision(1, 0)]
	public decimal? CD_SIT_CAG { get; set; }
}
