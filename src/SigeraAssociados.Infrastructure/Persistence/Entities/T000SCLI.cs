using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia a tabela legada dbo.T000SCLI.
/// Contém o catálogo de situações dos associados.
/// Os códigos e as descrições são lidos do banco selecionado.
/// </summary>
[Table("T000SCLI", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(CD_SIT_CLI))]
public sealed class T000SCLI
{
	/// <summary>
	/// Código da empresa.
	/// SQL Server: numeric(2,0), NOT NULL.
	/// Parte da chave primária e FK para T009EMPS.
	/// </summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>
	/// Código da situação do associado.
	/// SQL Server: numeric(2,0), NOT NULL.
	/// Parte da chave primária composta.
	/// </summary>
	[Column("CD_SIT_CLI", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_SIT_CLI { get; set; }

	/// <summary>
	/// Descrição da situação.
	/// SQL Server: varchar(30), NOT NULL.
	/// </summary>
	[Required]
	[MaxLength(30)]
	[Column("DE_SIT_CLI", TypeName = "varchar(30)")]
	public string DE_SIT_CLI { get; set; } = string.Empty;

	/// <summary>
	/// Empresa à qual a situação pertence.
	/// Relacionamento: T000SCLI.CD_EMP -> T009EMPS.CD_EMP.
	/// </summary>
	[ForeignKey(nameof(CD_EMP))]
	public T009EMPS? Empresa { get; set; }

	/// <summary>
	/// Associados que utilizam esta situação.
	/// A relação utiliza CD_EMP e CD_SIT_CLI.
	/// </summary>
	[InverseProperty(nameof(T000CLIE.Situacao))]
	public ICollection<T000CLIE> Associados { get; set; }
		= new List<T000CLIE>();
}