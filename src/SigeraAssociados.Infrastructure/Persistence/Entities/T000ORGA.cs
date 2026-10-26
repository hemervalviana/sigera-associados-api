using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000ORGA: órgãos (onde o associado
/// servidor público trabalha). Referenciada por T000RPOR.CD_ORG.
/// </summary>
[Table("T000ORGA", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_ORG))]
public sealed class T000ORGA
{
	/// <summary>Código da empresa. NOT NULL.</summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código do órgão. NOT NULL.</summary>
	[Column("CD_ORG", TypeName = "numeric(6, 0)")]
	[Precision(6, 0)]
	public decimal CD_ORG { get; set; }

	/// <summary>Grupo do órgão.</summary>
	[Column("CD_GP", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? CD_GP { get; set; }

	/// <summary>Nome do órgão. NOT NULL.</summary>
	[Required]
	[MaxLength(50)]
	[Column("NM_ORG", TypeName = "varchar(50)")]
	public string NM_ORG { get; set; } = string.Empty;

	[MaxLength(2)]
	[Column("SG_UF", TypeName = "varchar(2)")]
	public string? SG_UF { get; set; }

	/// <summary>Número do órgão.</summary>
	[MaxLength(7)]
	[Column("NR_ORG", TypeName = "varchar(7)")]
	public string? NR_ORG { get; set; }

	/// <summary>Subórgão (T009SBOR).</summary>
	[Column("SQ_SBOR", TypeName = "decimal(3, 0)")]
	[Precision(3, 0)]
	public decimal? SQ_SBOR { get; set; }

	/// <summary>Sigla.</summary>
	[MaxLength(100)]
	[Column("SGL_ORG", TypeName = "varchar(100)")]
	public string? SGL_ORG { get; set; }

	[MaxLength(100)]
	[Column("NM_LOG_ORG", TypeName = "varchar(100)")]
	public string? NM_LOG_ORG { get; set; }

	[Column("NR_LOG_ORG", TypeName = "numeric(5, 0)")]
	[Precision(5, 0)]
	public decimal? NR_LOG_ORG { get; set; }

	[MaxLength(50)]
	[Column("CP_EN_ORG", TypeName = "varchar(50)")]
	public string? CP_EN_ORG { get; set; }

	[MaxLength(50)]
	[Column("NM_BAI_ORG", TypeName = "varchar(50)")]
	public string? NM_BAI_ORG { get; set; }

	[Column("NR_CEP_ORG", TypeName = "numeric(8, 0)")]
	[Precision(8, 0)]
	public decimal? NR_CEP_ORG { get; set; }

	/// <summary>Município do órgão (T000MUNI).</summary>
	[Column("CD_MUN_ORG", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal? CD_MUN_ORG { get; set; }

	/// <summary>Contato administrativo.</summary>
	[MaxLength(100)]
	[Column("NM_CON_ADM", TypeName = "varchar(100)")]
	public string? NM_CON_ADM { get; set; }

	[Column("NR_FN_ADM", TypeName = "numeric(15, 0)")]
	[Precision(15, 0)]
	public decimal? NR_FN_ADM { get; set; }

	[MaxLength(100)]
	[Column("NM_EML_ADM", TypeName = "varchar(100)")]
	public string? NM_EML_ADM { get; set; }

	/// <summary>Contato financeiro.</summary>
	[MaxLength(100)]
	[Column("NM_CON_FIN", TypeName = "varchar(100)")]
	public string? NM_CON_FIN { get; set; }

	[Column("NR_FN_FIN", TypeName = "numeric(15, 0)")]
	[Precision(15, 0)]
	public decimal? NR_FN_FIN { get; set; }

	[MaxLength(100)]
	[Column("NM_EML_FIN", TypeName = "varchar(100)")]
	public string? NM_EML_FIN { get; set; }

	/// <summary>Empresa processadora (EP).</summary>
	[MaxLength(200)]
	[Column("NM_EMP_PRO", TypeName = "varchar(200)")]
	public string? NM_EMP_PRO { get; set; }

	[MaxLength(100)]
	[Column("NM_CON_EP", TypeName = "varchar(100)")]
	public string? NM_CON_EP { get; set; }

	[MaxLength(100)]
	[Column("NM_LOG_EP", TypeName = "varchar(100)")]
	public string? NM_LOG_EP { get; set; }

	[Column("NR_LOG_EP", TypeName = "numeric(5, 0)")]
	[Precision(5, 0)]
	public decimal? NR_LOG_EP { get; set; }

	[MaxLength(50)]
	[Column("CP_EN_EP", TypeName = "varchar(50)")]
	public string? CP_EN_EP { get; set; }

	[MaxLength(50)]
	[Column("NM_BAI_EP", TypeName = "varchar(50)")]
	public string? NM_BAI_EP { get; set; }

	[Column("NR_CEP_EP", TypeName = "numeric(8, 0)")]
	[Precision(8, 0)]
	public decimal? NR_CEP_EP { get; set; }

	/// <summary>Município da empresa processadora (T000MUNI).</summary>
	[Column("CD_MUN_EP", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal? CD_MUN_EP { get; set; }

	[Column("NR_FN_EP", TypeName = "numeric(15, 0)")]
	[Precision(15, 0)]
	public decimal? NR_FN_EP { get; set; }

	[MaxLength(100)]
	[Column("NM_EML_EP", TypeName = "varchar(100)")]
	public string? NM_EML_EP { get; set; }

	/// <summary>Data limite de processamento.</summary>
	[Column("DT_LIM_PRO", TypeName = "datetime")]
	public DateTime? DT_LIM_PRO { get; set; }

	/// <summary>Data de pagamento.</summary>
	[Column("DT_PAG", TypeName = "datetime")]
	public DateTime? DT_PAG { get; set; }

	/// <summary>Filial (T000PARA).</summary>
	[Column("CD_FIL", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? CD_FIL { get; set; }

	/// <summary>Situação do órgão (0 a 9; sem tabela no banco).</summary>
	[Column("CD_SIT_ORG", TypeName = "numeric(1, 0)")]
	[Precision(1, 0)]
	public decimal? CD_SIT_ORG { get; set; }
}
