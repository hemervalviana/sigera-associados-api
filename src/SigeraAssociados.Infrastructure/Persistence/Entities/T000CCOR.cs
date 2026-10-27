using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000CCOR: contas bancárias da
/// pessoa (uma ou mais). Uma linha por conta (SQ_CTA_COR).
/// </summary>
[Table("T000CCOR", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES), nameof(SQ_CTA_COR))]
public sealed class T000CCOR
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Pessoa (T000PESS). NOT NULL.</summary>
	[Column("SQ_PES", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES { get; set; }

	/// <summary>Sequência da conta da pessoa (MAX + 1, até 99). NOT NULL.</summary>
	[Column("SQ_CTA_COR", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal SQ_CTA_COR { get; set; }

	/// <summary>Banco (T000BANC). NOT NULL.</summary>
	[Column("CD_BCO", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal CD_BCO { get; set; }

	[MaxLength(1)]
	[Column("DV_BCO", TypeName = "varchar(1)")]
	public string? DV_BCO { get; set; }

	/// <summary>Agência. NOT NULL.</summary>
	[Column("CD_AG", TypeName = "numeric(5, 0)")]
	[Precision(5, 0)]
	public decimal CD_AG { get; set; }

	[MaxLength(1)]
	[Column("DV_AG", TypeName = "varchar(1)")]
	public string? DV_AG { get; set; }

	/// <summary>Número da conta. NOT NULL.</summary>
	[Column("NR_CTA_COR", TypeName = "numeric(13, 0)")]
	[Precision(13, 0)]
	public decimal NR_CTA_COR { get; set; }

	[MaxLength(2)]
	[Column("DV_CTA_COR", TypeName = "varchar(2)")]
	public string? DV_CTA_COR { get; set; }

	[MaxLength(25)]
	[Column("NR_IDT", TypeName = "varchar(25)")]
	public string? NR_IDT { get; set; }

	/// <summary>Operação (ex.: 001, 013 na Caixa).</summary>
	[MaxLength(5)]
	[Column("OP_BCO", TypeName = "varchar(5)")]
	public string? OP_BCO { get; set; }

	[MaxLength(25)]
	[Column("NR_IDT_02", TypeName = "varchar(25)")]
	public string? NR_IDT_02 { get; set; }

	/// <summary>Titular da conta.</summary>
	[MaxLength(100)]
	[Column("DE_TIT_COR", TypeName = "varchar(100)")]
	public string? DE_TIT_COR { get; set; }

	/// <summary>Tipo de conta.</summary>
	[Column("TP_CTA_COR", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? TP_CTA_COR { get; set; }

	[Column("env_opt_cax", TypeName = "decimal(1, 0)")]
	[Precision(1, 0)]
	public decimal? ENV_OPT_CAX { get; set; }

	/// <summary>Indicador de conta ativa.</summary>
	[Column("ID_CC_ATV", TypeName = "numeric(1, 0)")]
	[Precision(1, 0)]
	public decimal? ID_CC_ATV { get; set; }

	/// <summary>Chave PIX.</summary>
	[MaxLength(100)]
	[Column("DE_PIX", TypeName = "varchar(100)")]
	public string? DE_PIX { get; set; }
}
