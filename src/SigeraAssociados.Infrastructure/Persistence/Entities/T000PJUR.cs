using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000PJUR: cadastro complementar
/// da pessoa jurídica (T000PESS com TP_PES = 'J'). Gravada pelo CRUD de
/// corretores; o associado continua só pessoa física.
/// </summary>
[Table("T000PJUR", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES_JUR))]
public sealed class T000PJUR
{
	/// <summary>Código da empresa. Referencia T000PESS.CD_EMP.</summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Pessoa jurídica. Referencia T000PESS.SQ_PES. NOT NULL.</summary>
	[Column("SQ_PES_JUR", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES_JUR { get; set; }

	/// <summary>CNPJ (14 números, sem os zeros à esquerda).</summary>
	[Column("NR_CPJ", TypeName = "numeric(14, 0)")]
	[Precision(14, 0)]
	public decimal? NR_CPJ { get; set; }

	/// <summary>Inscrição estadual (CGF).</summary>
	[Column("NR_CGF", TypeName = "numeric(9, 0)")]
	[Precision(9, 0)]
	public decimal? NR_CGF { get; set; }

	/// <summary>Nome do contato.</summary>
	[MaxLength(50)]
	[Column("NM_CON", TypeName = "varchar(50)")]
	public string? NM_CON { get; set; }

	/// <summary>CD_CG (código do legado, até 4 dígitos).</summary>
	[Column("CD_CG", TypeName = "numeric(4, 0)")]
	[Precision(4, 0)]
	public decimal? CD_CG { get; set; }
}
