using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000REFE: referências pessoais
/// da pessoa (uma ou mais). Uma linha por referência (SQ_REF).
/// </summary>
[Table("T000REFE", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES), nameof(SQ_REF))]
public sealed class T000REFE
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Pessoa (T000PESS). NOT NULL.</summary>
	[Column("SQ_PES", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES { get; set; }

	/// <summary>Sequência da referência da pessoa (MAX + 1, até 9999). NOT NULL.</summary>
	[Column("SQ_REF", TypeName = "numeric(4, 0)")]
	[Precision(4, 0)]
	public decimal SQ_REF { get; set; }

	/// <summary>Nome da referência. NOT NULL.</summary>
	[Required]
	[MaxLength(50)]
	[Column("NM_REF", TypeName = "varchar(50)")]
	public string NM_REF { get; set; } = string.Empty;

	/// <summary>Data de nascimento.</summary>
	[Column("DT_NAS_REF", TypeName = "datetime")]
	public DateTime? DT_NAS_REF { get; set; }

	/// <summary>Telefone 1 (DDD + número). NOT NULL.</summary>
	[Required]
	[MaxLength(12)]
	[Column("NR_FN1_REF", TypeName = "varchar(12)")]
	public string NR_FN1_REF { get; set; } = string.Empty;

	/// <summary>Telefone 2 (DDD + número).</summary>
	[MaxLength(12)]
	[Column("NR_FN2_REF", TypeName = "varchar(12)")]
	public string? NR_FN2_REF { get; set; }
}
