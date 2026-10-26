using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T009SBOR: subórgãos.
/// Referenciada por T000ORGA.SQ_SBOR.
/// SQ_SBOR é IDENTITY: a inclusão é feita por SQL (INSERT sem o código +
/// SCOPE_IDENTITY), nunca pelo Entity Framework.
/// </summary>
[Table("T009SBOR", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_SBOR))]
public sealed class T009SBOR
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código do subórgão. IDENTITY, NOT NULL.</summary>
	[Column("SQ_SBOR", TypeName = "decimal(3, 0)")]
	[Precision(3, 0)]
	public decimal SQ_SBOR { get; set; }

	/// <summary>Nome. NOT NULL.</summary>
	[Required]
	[MaxLength(100)]
	[Column("NM_SBOR", TypeName = "varchar(100)")]
	public string NM_SBOR { get; set; } = string.Empty;
}
