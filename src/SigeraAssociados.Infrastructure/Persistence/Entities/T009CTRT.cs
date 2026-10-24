using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeamento parcial (somente leitura) da tabela legada dbo.T009CTRT.
/// Contém apenas as colunas necessárias para identificar
/// a carteira de associados de cada corretor.
/// Não cria nem modifica a tabela existente.
/// </summary>
[Table("T009CTRT", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_FIL), nameof(SQ_CTR))]
public sealed class T009CTRT
{
	/// <summary>
	/// Código da empresa. SQL Server: numeric(2,0), NOT NULL.
	/// </summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>
	/// Código da filial do contrato. SQL Server: numeric(2,0), NOT NULL.
	/// </summary>
	[Column("CD_FIL", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_FIL { get; set; }

	/// <summary>
	/// Sequência do contrato.
	/// </summary>
	[Column("SQ_CTR", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_CTR { get; set; }

	/// <summary>
	/// Pessoa associada (cliente) do contrato. Referencia T000CLIE.SQ_PES_CLI.
	/// </summary>
	[Column("SQ_PES_CLI", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES_CLI { get; set; }

	/// <summary>
	/// Pessoa corretor do contrato.
	/// </summary>
	[Column("SQ_PES_CRO", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES_CRO { get; set; }
}
