using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeamento parcial (somente leitura) da tabela legada dbo.T000PJUR,
/// cadastro complementar da pessoa jurídica.
/// Contém apenas a chave, usada para validar o cadastro do associado:
/// pessoa com TP_PES = 'J' precisa existir nesta tabela.
/// Não cria nem modifica a tabela existente.
/// </summary>
[Table("T000PJUR", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES_JUR))]
public sealed class T000PJUR
{
	/// <summary>
	/// Código da empresa. Referencia T000PESS.CD_EMP.
	/// </summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>
	/// Pessoa jurídica. Referencia T000PESS.SQ_PES.
	/// </summary>
	[Column("SQ_PES_JUR", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES_JUR { get; set; }
}
