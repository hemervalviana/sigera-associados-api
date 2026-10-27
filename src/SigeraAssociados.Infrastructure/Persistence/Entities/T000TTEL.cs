using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000TTEL: tipos de telefone
/// (residencial, celular...). Referenciada por T000TELE.CD_TP_FN.
/// </summary>
[Table("T000TTEL", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_TP_FN))]
public sealed class T000TTEL
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código do tipo. NOT NULL.</summary>
	[Column("CD_TP_FN", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_TP_FN { get; set; }

	/// <summary>Descrição. NOT NULL.</summary>
	[Required]
	[MaxLength(20)]
	[Column("DE_TP_FN", TypeName = "varchar(20)")]
	public string DE_TP_FN { get; set; } = string.Empty;
}
