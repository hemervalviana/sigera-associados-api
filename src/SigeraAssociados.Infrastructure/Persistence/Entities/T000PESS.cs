using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000PESS.
/// Não cria nem modifica a tabela existente.
/// </summary>
[Table("T000PESS", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES))]
public sealed class T000PESS
{
	/// <summary>
	/// Código da empresa.
	/// SQL Server: numeric(2,0), NOT NULL.
	/// Parte da chave primária e da FK para T009EMPS.
	/// </summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>
	/// Código sequencial da pessoa.
	/// SQL Server: numeric(7,0), NOT NULL.
	/// Parte da chave primária composta.
	/// </summary>
	[Column("SQ_PES", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES { get; set; }

	/// <summary>
	/// Tipo da pessoa: F = física; J = jurídica.
	/// SQL Server: varchar(1), NOT NULL.
	/// </summary>
	[Required]
	[MaxLength(1)]
	[Column("TP_PES", TypeName = "varchar(1)")]
	public string TP_PES { get; set; } = string.Empty;

	/// <summary>
	/// Data de cadastro.
	/// SQL Server: datetime, NOT NULL.
	/// </summary>
	[Required]
	[Column("DT_CAD", TypeName = "datetime")]
	public DateTime DT_CAD { get; set; }

	/// <summary>
	/// Nome da pessoa.
	/// SQL Server: varchar(80), NOT NULL.
	/// </summary>
	[Required]
	[MaxLength(80)]
	[Column("NM_PES", TypeName = "varchar(80)")]
	public string NM_PES { get; set; } = string.Empty;

	/// <summary>
	/// Nome curto.
	/// SQL Server: varchar(20), NULL.
	/// </summary>
	[MaxLength(20)]
	[Column("NC_PES", TypeName = "varchar(20)")]
	public string? NC_PES { get; set; }

	/// <summary>
	/// Observações.
	/// SQL Server: varchar(255), NULL.
	/// </summary>
	[MaxLength(255)]
	[Column("OB_PES", TypeName = "varchar(255)")]
	public string? OB_PES { get; set; }

	/// <summary>
	/// Endereço de e-mail.
	/// SQL Server: varchar(50), NULL.
	/// </summary>
	[MaxLength(50)]
	[Column("DE_COR_ELE", TypeName = "varchar(50)")]
	public string? DE_COR_ELE { get; set; }

	/// <summary>
	/// Endereço de site.
	/// SQL Server: varchar(50), NULL.
	/// </summary>
	[MaxLength(50)]
	[Column("DE_URL", TypeName = "varchar(50)")]
	public string? DE_URL { get; set; }

	/// <summary>
	/// Nome do usuário relacionado ao cadastro.
	/// SQL Server: varchar(20), NULL.
	/// </summary>
	[MaxLength(20)]
	[Column("NM_USU", TypeName = "varchar(20)")]
	public string? NM_USU { get; set; }

	/// <summary>
	/// Empresa à qual a pessoa pertence.
	/// Relacionamento: T000PESS.CD_EMP -> T009EMPS.CD_EMP.
	/// </summary>
	[ForeignKey(nameof(CD_EMP))]
	public T009EMPS? Empresa { get; set; }
}