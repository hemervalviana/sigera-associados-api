using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000ENDE: endereços da pessoa
/// (um ou mais). Uma linha por endereço (SQ_EN_PES).
/// </summary>
[Table("T000ENDE", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES), nameof(SQ_EN_PES))]
public sealed class T000ENDE
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Pessoa (T000PESS). NOT NULL.</summary>
	[Column("SQ_PES", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES { get; set; }

	/// <summary>Sequência do endereço da pessoa (MAX + 1, até 99). NOT NULL.</summary>
	[Column("SQ_EN_PES", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal SQ_EN_PES { get; set; }

	/// <summary>Tipo de endereço (T000TEND). NOT NULL.</summary>
	[Column("CD_TP_EN", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_TP_EN { get; set; }

	/// <summary>Logradouro. NOT NULL.</summary>
	[Required]
	[MaxLength(50)]
	[Column("NM_LOG", TypeName = "varchar(50)")]
	public string NM_LOG { get; set; } = string.Empty;

	/// <summary>Número.</summary>
	[Column("NR_LOG", TypeName = "numeric(10, 0)")]
	[Precision(10, 0)]
	public decimal? NR_LOG { get; set; }

	/// <summary>Complemento.</summary>
	[MaxLength(100)]
	[Column("CP_EN", TypeName = "varchar(100)")]
	public string? CP_EN { get; set; }

	/// <summary>Bairro.</summary>
	[MaxLength(30)]
	[Column("NM_BAI", TypeName = "varchar(30)")]
	public string? NM_BAI { get; set; }

	/// <summary>CEP.</summary>
	[Column("NR_CEP", TypeName = "numeric(8, 0)")]
	[Precision(8, 0)]
	public decimal? NR_CEP { get; set; }

	/// <summary>Endereço principal (S/N). NOT NULL.</summary>
	[Required]
	[MaxLength(1)]
	[Column("ID_EN_PCP", TypeName = "varchar(1)")]
	public string ID_EN_PCP { get; set; } = string.Empty;

	/// <summary>Município (T000MUNI). NOT NULL.</summary>
	[Column("CD_MUN", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal CD_MUN { get; set; }
}
