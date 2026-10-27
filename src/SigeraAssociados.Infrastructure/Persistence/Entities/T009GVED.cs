using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T009GVED: gerentes de vendas.
/// Referenciada (sem FK) por T009CRRT.SQ_GVED.
/// </summary>
[Table("T009GVED", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_GVED))]
public sealed class T009GVED
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código do gerente de vendas. NOT NULL.</summary>
	[Column("SQ_GVED", TypeName = "numeric(8, 0)")]
	[Precision(8, 0)]
	public decimal SQ_GVED { get; set; }

	/// <summary>Nome do gerente.</summary>
	[MaxLength(100)]
	[Column("DE_GVED", TypeName = "varchar(100)")]
	public string? DE_GVED { get; set; }

	/// <summary>Percentual de comissão.</summary>
	[Column("VR_PC_CMS", TypeName = "decimal(5, 2)")]
	[Precision(5, 2)]
	public decimal? VR_PC_CMS { get; set; }
}
