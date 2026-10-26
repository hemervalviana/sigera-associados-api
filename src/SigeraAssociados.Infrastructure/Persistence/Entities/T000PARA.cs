using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia a tabela legada dbo.T000PARA: cadastro das filiais da empresa
/// (uma linha por filial). Somente leitura.
/// As colunas NM_ARQ_LM, NR_POS_X, NR_POS_Y, VR_ALT e VR_LAR não são
/// usadas pelo sistema e ficam fora do mapeamento.
/// </summary>
[Table("T000PARA", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_FIL))]
public sealed class T000PARA
{
	/// <summary>
	/// Código da empresa. SQL Server: numeric(2,0), NOT NULL.
	/// </summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>
	/// Código da filial. SQL Server: cd_fil numeric(2,0), NOT NULL.
	/// </summary>
	[Column("cd_fil", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_FIL { get; set; }

	/// <summary>
	/// Nome da filial. SQL Server: varchar(80), NOT NULL.
	/// </summary>
	[Required]
	[MaxLength(80)]
	[Column("NM_EMP", TypeName = "varchar(80)")]
	public string NM_EMP { get; set; } = string.Empty;

	/// <summary>
	/// Nome curto. SQL Server: varchar(20), NULL.
	/// </summary>
	[MaxLength(20)]
	[Column("NC_EMP", TypeName = "varchar(20)")]
	public string? NC_EMP { get; set; }

	[MaxLength(50)]
	[Column("NM_LOG_EMP", TypeName = "varchar(50)")]
	public string? NM_LOG_EMP { get; set; }

	[Column("NR_LOG_EMP", TypeName = "numeric(5, 0)")]
	[Precision(5, 0)]
	public decimal? NR_LOG_EMP { get; set; }

	[MaxLength(30)]
	[Column("CP_EN_EMP", TypeName = "varchar(30)")]
	public string? CP_EN_EMP { get; set; }

	[MaxLength(30)]
	[Column("NM_BAI_EMP", TypeName = "varchar(30)")]
	public string? NM_BAI_EMP { get; set; }

	[Column("NR_CEP_EMP", TypeName = "numeric(8, 0)")]
	[Precision(8, 0)]
	public decimal? NR_CEP_EMP { get; set; }

	/// <summary>
	/// Município. SQL Server: varchar(55), NULL.
	/// </summary>
	[MaxLength(55)]
	[Column("DE_MUN_EMP", TypeName = "varchar(55)")]
	public string? DE_MUN_EMP { get; set; }

	/// <summary>
	/// UF. SQL Server: varchar(2), NULL.
	/// </summary>
	[MaxLength(2)]
	[Column("SG_UF_EMP", TypeName = "varchar(2)")]
	public string? SG_UF_EMP { get; set; }

	[Column("NR_DDD_FN", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? NR_DDD_FN { get; set; }

	[MaxLength(12)]
	[Column("NR_FN", TypeName = "varchar(12)")]
	public string? NR_FN { get; set; }

	[Column("NR_DDD_FAX", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? NR_DDD_FAX { get; set; }

	[MaxLength(12)]
	[Column("NR_FAX", TypeName = "varchar(12)")]
	public string? NR_FAX { get; set; }

	[Column("NR_CPJ", TypeName = "numeric(14, 0)")]
	[Precision(14, 0)]
	public decimal? NR_CPJ { get; set; }
}
