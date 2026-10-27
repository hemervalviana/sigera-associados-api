using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T009EXTRA: créditos do associado.
/// Uma linha por crédito (SQ_PGT = MAX + 1 do associado).
/// FKs: contrato (T009CTRT) e averbação (T009RBSR), ambas com a filial.
/// </summary>
[Table("T009EXTRA", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES_CLI), nameof(SQ_PGT))]
public sealed class T009EXTRA
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Associado. NOT NULL.</summary>
	[Column("SQ_PES_CLI", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES_CLI { get; set; }

	/// <summary>Sequência do crédito do associado. NOT NULL.</summary>
	[Column("SQ_PGT", TypeName = "numeric(8, 0)")]
	[Precision(8, 0)]
	public decimal SQ_PGT { get; set; }

	/// <summary>Data do crédito.</summary>
	[Column("DT_PGT", TypeName = "datetime")]
	public DateTime? DT_PGT { get; set; }

	/// <summary>Valor do crédito.</summary>
	[Column("VR_PGT", TypeName = "numeric(11, 2)")]
	[Precision(11, 2)]
	public decimal? VR_PGT { get; set; }

	/// <summary>Número do cheque.</summary>
	[Column("NR_CHQ", TypeName = "numeric(10, 0)")]
	[Precision(10, 0)]
	public decimal? NR_CHQ { get; set; }

	/// <summary>Forma de pagamento (T009FRPG.TP_FMA_PGT). NOT NULL.</summary>
	[Column("TP_PGT", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal TP_PGT { get; set; }

	/// <summary>Tipo de baixa: 1 automática, 2 manual, 4 crédito. NOT NULL.</summary>
	[Column("ID_PGT_AUT", TypeName = "numeric(1, 0)")]
	[Precision(1, 0)]
	public decimal ID_PGT_AUT { get; set; }

	/// <summary>Observação.</summary>
	[MaxLength(100)]
	[Column("DE_OBS", TypeName = "varchar(100)")]
	public string? DE_OBS { get; set; }

	/// <summary>Filial.</summary>
	[Column("CD_FIL", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? CD_FIL { get; set; }

	/// <summary>Contrato.</summary>
	[Column("SQ_CTR", TypeName = "numeric(8, 0)")]
	[Precision(8, 0)]
	public decimal? SQ_CTR { get; set; }

	/// <summary>Número da parcela.</summary>
	[Column("NR_PCL", TypeName = "numeric(5, 0)")]
	[Precision(5, 0)]
	public decimal? NR_PCL { get; set; }

	/// <summary>Usuário do legado (a API não preenche).</summary>
	[Column("CD_USU", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? CD_USU { get; set; }

	/// <summary>Saldo do crédito. NOT NULL.</summary>
	[Column("VR_SLD", TypeName = "numeric(11, 2)")]
	[Precision(11, 2)]
	public decimal VR_SLD { get; set; }

	/// <summary>Convênio.</summary>
	[Column("CD_CVN", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? CD_CVN { get; set; }

	/// <summary>Código da averbação.</summary>
	[Column("CD_AVB", TypeName = "numeric(5, 0)")]
	[Precision(5, 0)]
	public decimal? CD_AVB { get; set; }

	/// <summary>Matrícula.</summary>
	[MaxLength(20)]
	[Column("MT_PES", TypeName = "varchar(20)")]
	public string? MT_PES { get; set; }

	/// <summary>Dígito da matrícula.</summary>
	[MaxLength(5)]
	[Column("DV_MT", TypeName = "varchar(5)")]
	public string? DV_MT { get; set; }

	/// <summary>Produto.</summary>
	[Column("CD_PRD", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? CD_PRD { get; set; }
}
