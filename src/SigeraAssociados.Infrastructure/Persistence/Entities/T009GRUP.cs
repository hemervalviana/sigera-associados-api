using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T009GRUP: grupos de órgãos.
/// Referenciada por T000ORGA.CD_GP.
/// </summary>
[Table("T009GRUP", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_GP))]
public sealed class T009GRUP
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código do grupo. NOT NULL.</summary>
	[Column("CD_GP", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal CD_GP { get; set; }

	/// <summary>Descrição. NOT NULL.</summary>
	[Required]
	[MaxLength(30)]
	[Column("DE_GP", TypeName = "varchar(30)")]
	public string DE_GP { get; set; } = string.Empty;

	/// <summary>Tipo do grupo (1 caractere). NOT NULL.</summary>
	[Required]
	[MaxLength(1)]
	[Column("TP_GP", TypeName = "varchar(1)")]
	public string TP_GP { get; set; } = string.Empty;

	/// <summary>Indicador de ordem (1 caractere). NOT NULL.</summary>
	[Required]
	[MaxLength(1)]
	[Column("ID_ORD", TypeName = "varchar(1)")]
	public string ID_ORD { get; set; } = string.Empty;

	/// <summary>Número do dia (0 a 99). NOT NULL.</summary>
	[Column("NR_DD", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal NR_DD { get; set; }
}
