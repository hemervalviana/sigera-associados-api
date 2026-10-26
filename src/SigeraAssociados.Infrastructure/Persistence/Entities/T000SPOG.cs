using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000SPOG: situações do vínculo
/// do associado com o órgão. Referenciada por T000RPOR.CD_SIT_ORG.
/// </summary>
[Table("T000SPOG", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_SIT_ORG))]
public sealed class T000SPOG
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código da situação. NOT NULL.</summary>
	[Column("CD_SIT_ORG", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_SIT_ORG { get; set; }

	/// <summary>Descrição. NOT NULL.</summary>
	[Required]
	[MaxLength(35)]
	[Column("DE_SIT_ORG", TypeName = "varchar(35)")]
	public string DE_SIT_ORG { get; set; } = string.Empty;
}
