using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000TELE: telefones da pessoa
/// (um ou mais). Uma linha por telefone (SQ_TELE).
/// </summary>
[Table("T000TELE", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES), nameof(SQ_TELE))]
public sealed class T000TELE
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Pessoa (T000PESS). NOT NULL.</summary>
	[Column("SQ_PES", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES { get; set; }

	/// <summary>
	/// Sequência do telefone (MAX + 1 da empresa). NOT NULL, default -1 no banco;
	/// a API sempre informa o valor.
	/// </summary>
	[Column("SQ_TELE", TypeName = "numeric(18, 0)")]
	[Precision(18, 0)]
	[DatabaseGenerated(DatabaseGeneratedOption.None)]
	public decimal SQ_TELE { get; set; }

	/// <summary>DDD. NOT NULL.</summary>
	[Column("NR_DDD", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal NR_DDD { get; set; }

	/// <summary>Número do telefone (somente dígitos). NOT NULL.</summary>
	[Required]
	[MaxLength(12)]
	[Column("NR_FN", TypeName = "varchar(12)")]
	public string NR_FN { get; set; } = string.Empty;

	/// <summary>Tipo de telefone (T000TTEL).</summary>
	[Column("CD_TP_FN", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? CD_TP_FN { get; set; }
}
