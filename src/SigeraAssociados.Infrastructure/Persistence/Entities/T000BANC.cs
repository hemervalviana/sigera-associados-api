using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000BANC: bancos.
/// Referenciada por T000CCOR.CD_BCO.
/// </summary>
[Table("T000BANC", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_BCO))]
public sealed class T000BANC
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código do banco (ex.: 001, 104, 237). NOT NULL.</summary>
	[Column("CD_BCO", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal CD_BCO { get; set; }

	/// <summary>Nome. NOT NULL.</summary>
	[Required]
	[MaxLength(35)]
	[Column("NM_BCO", TypeName = "varchar(35)")]
	public string NM_BCO { get; set; } = string.Empty;

	/// <summary>Situação do banco (0 a 9).</summary>
	[Column("CD_SIT_BCO", TypeName = "numeric(1, 0)")]
	[Precision(1, 0)]
	public decimal? CD_SIT_BCO { get; set; }
}
