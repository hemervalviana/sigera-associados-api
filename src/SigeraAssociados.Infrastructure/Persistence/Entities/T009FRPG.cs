using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T009FRPG: formas de pagamento.
/// Referenciada (sem FK) por T009EXTRA.TP_PGT.
/// </summary>
[Table("T009FRPG", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(TP_FMA_PGT))]
public sealed class T009FRPG
{
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>Código da forma de pagamento. NOT NULL.</summary>
	[Column("TP_FMA_PGT", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal TP_FMA_PGT { get; set; }

	/// <summary>Descrição.</summary>
	[MaxLength(100)]
	[Column("DE_FMA_PGT", TypeName = "varchar(100)")]
	public string? DE_FMA_PGT { get; set; }

	/// <summary>Gera saldo no crédito: 1 = sim; outro valor ou vazio = não.</summary>
	[Column("ID_GER_SLD", TypeName = "decimal(1, 0)")]
	[Precision(1, 0)]
	public decimal? ID_GER_SLD { get; set; }
}
