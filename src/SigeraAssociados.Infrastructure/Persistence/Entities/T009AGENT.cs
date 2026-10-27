using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T009AGENT: agentes do corretor,
/// com dados bancários. Referenciada por T009CRRT.SQ_AGEN (FK).
/// </summary>
[Table("T009AGENT", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_AGEN))]
public sealed class T009AGENT
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código do agente. NOT NULL.</summary>
	[Column("SQ_AGEN", TypeName = "numeric(8, 0)")]
	[Precision(8, 0)]
	public decimal SQ_AGEN { get; set; }

	/// <summary>Nome do agente.</summary>
	[MaxLength(100)]
	[Column("DE_AGEN", TypeName = "varchar(100)")]
	public string? DE_AGEN { get; set; }

	[Column("CD_BCO", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? CD_BCO { get; set; }

	[MaxLength(1)]
	[Column("DV_BCO", TypeName = "varchar(1)")]
	public string? DV_BCO { get; set; }

	[Column("CD_AG", TypeName = "numeric(5, 0)")]
	[Precision(5, 0)]
	public decimal? CD_AG { get; set; }

	[MaxLength(1)]
	[Column("DV_AG", TypeName = "varchar(1)")]
	public string? DV_AG { get; set; }

	[Column("NR_CTA_COR", TypeName = "numeric(13, 0)")]
	[Precision(13, 0)]
	public decimal? NR_CTA_COR { get; set; }

	[MaxLength(2)]
	[Column("DV_CTA_COR", TypeName = "varchar(2)")]
	public string? DV_CTA_COR { get; set; }

	/// <summary>Operação bancária.</summary>
	[Column("OP_BCO", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? OP_BCO { get; set; }
}
