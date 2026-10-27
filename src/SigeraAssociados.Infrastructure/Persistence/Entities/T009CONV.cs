using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T009CONV: convênios.
/// FKs: banco (T000BANC), filial (T000PARA) e conta corrente (T009CONT).
/// </summary>
[Table("T009CONV", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_CVN))]
public sealed class T009CONV
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código do convênio. NOT NULL.</summary>
	[Column("CD_CVN", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal CD_CVN { get; set; }

	/// <summary>Nome. NOT NULL.</summary>
	[Required]
	[MaxLength(35)]
	[Column("NM_CVN", TypeName = "varchar(35)")]
	public string NM_CVN { get; set; } = string.Empty;

	[MaxLength(15)]
	[Column("NR_CVN", TypeName = "varchar(15)")]
	public string? NR_CVN { get; set; }

	/// <summary>Diretório de remessa.</summary>
	[MaxLength(40)]
	[Column("NM_DRT_REM", TypeName = "varchar(40)")]
	public string? NM_DRT_REM { get; set; }

	/// <summary>Diretório de retorno.</summary>
	[MaxLength(40)]
	[Column("NM_DRT_ROR", TypeName = "varchar(40)")]
	public string? NM_DRT_ROR { get; set; }

	/// <summary>Extensão dos arquivos.</summary>
	[MaxLength(5)]
	[Column("DE_EXT_ARQ", TypeName = "varchar(5)")]
	public string? DE_EXT_ARQ { get; set; }

	/// <summary>Banco (T000BANC).</summary>
	[Column("CD_BCO", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? CD_BCO { get; set; }

	[MaxLength(15)]
	[Column("NR_CV2", TypeName = "varchar(15)")]
	public string? NR_CV2 { get; set; }

	[MaxLength(15)]
	[Column("NR_CV3", TypeName = "varchar(15)")]
	public string? NR_CV3 { get; set; }

	/// <summary>Código da empresa no convênio.</summary>
	[Column("CD_EMPR", TypeName = "numeric(20, 0)")]
	[Precision(20, 0)]
	public decimal? CD_EMPR { get; set; }

	/// <summary>Carteira.</summary>
	[Column("CD_CART", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? CD_CART { get; set; }

	/// <summary>Conta corrente (T009CONT).</summary>
	[Column("SQ_CTA_COR", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? SQ_CTA_COR { get; set; }

	/// <summary>Parâmetro de transmissão.</summary>
	[Column("PAR_TRSM", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? PAR_TRSM { get; set; }

	/// <summary>Código de compensação.</summary>
	[Column("CD_COMP", TypeName = "numeric(4, 0)")]
	[Precision(4, 0)]
	public decimal? CD_COMP { get; set; }

	[Column("CD_CNAB", TypeName = "numeric(18, 0)")]
	[Precision(18, 0)]
	public decimal? CD_CNAB { get; set; }

	[Column("CT_TP_GOV", TypeName = "numeric(10, 0)")]
	[Precision(10, 0)]
	public decimal? CT_TP_GOV { get; set; }

	[MaxLength(100)]
	[Column("DE_ODBC", TypeName = "varchar(100)")]
	public string? DE_ODBC { get; set; }

	/// <summary>Filial (T000PARA).</summary>
	[Column("CD_FIL", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? CD_FIL { get; set; }

	/// <summary>Situação (valores do legado, 0 a 9).</summary>
	[Column("CD_SIT_CON", TypeName = "numeric(1, 0)")]
	[Precision(1, 0)]
	public decimal? CD_SIT_CON { get; set; }
}
