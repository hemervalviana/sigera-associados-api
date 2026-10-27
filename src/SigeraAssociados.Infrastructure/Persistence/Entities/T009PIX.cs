using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T009PIX: chaves Pix da pessoa
/// (uma ou mais). Uma linha por chave (SQ_REG).
/// </summary>
[Table("T009PIX", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES), nameof(SQ_REG))]
public sealed class T009PIX
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Pessoa (T000PESS). NOT NULL.</summary>
	[Column("SQ_PES", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES { get; set; }

	/// <summary>Sequência do registro da pessoa (MAX + 1, até 99). NOT NULL.</summary>
	[Column("SQ_REG", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal SQ_REG { get; set; }

	/// <summary>Tipo da chave: 1 CPF/CNPJ, 2 CELULAR, 3 E-MAIL, 4 ALEATÓRIA. NOT NULL.</summary>
	[Column("CD_PIX", TypeName = "smallint")]
	public short CD_PIX { get; set; }

	/// <summary>Chave Pix. NOT NULL.</summary>
	[Required]
	[Column("VL_CHAVE", TypeName = "nvarchar(max)")]
	public string VL_CHAVE { get; set; } = string.Empty;
}
