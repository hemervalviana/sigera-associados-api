using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000RPOR: vínculos da pessoa
/// (associado) com os órgãos em que trabalha. Uma linha por vínculo (SQ_ORG).
/// </summary>
[Table("T000RPOR", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES), nameof(SQ_ORG))]
public sealed class T000RPOR
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Pessoa (T000PESS). NOT NULL.</summary>
	[Column("SQ_PES", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES { get; set; }

	/// <summary>Sequência do vínculo da pessoa (MAX + 1). NOT NULL.</summary>
	[Column("SQ_ORG", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal SQ_ORG { get; set; }

	/// <summary>Órgão (T000ORGA). NOT NULL.</summary>
	[Column("CD_ORG", TypeName = "numeric(6, 0)")]
	[Precision(6, 0)]
	public decimal CD_ORG { get; set; }

	/// <summary>Lotação (T000LOTA).</summary>
	[Column("CD_LOT", TypeName = "decimal(6, 0)")]
	[Precision(6, 0)]
	public decimal? CD_LOT { get; set; }

	/// <summary>Situação do vínculo (T000SPOG).</summary>
	[Column("CD_SIT_ORG", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? CD_SIT_ORG { get; set; }

	/// <summary>Cargo.</summary>
	[Column("CD_CG", TypeName = "numeric(4, 0)")]
	[Precision(4, 0)]
	public decimal? CD_CG { get; set; }

	/// <summary>Matrícula.</summary>
	[MaxLength(20)]
	[Column("MT_PES", TypeName = "varchar(20)")]
	public string? MT_PES { get; set; }

	/// <summary>Salário bruto.</summary>
	[Column("VR_SAL", TypeName = "numeric(9, 2)")]
	[Precision(9, 2)]
	public decimal? VR_SAL { get; set; }

	/// <summary>Dígito da matrícula.</summary>
	[MaxLength(5)]
	[Column("DV_MT", TypeName = "varchar(5)")]
	public string? DV_MT { get; set; }

	/// <summary>Matrícula do instituidor.</summary>
	[MaxLength(20)]
	[Column("MT_INS", TypeName = "varchar(20)")]
	public string? MT_INS { get; set; }

	/// <summary>Salário líquido.</summary>
	[Column("VR_SAL_LIQ", TypeName = "numeric(9, 2)")]
	[Precision(9, 2)]
	public decimal? VR_SAL_LIQ { get; set; }
}
