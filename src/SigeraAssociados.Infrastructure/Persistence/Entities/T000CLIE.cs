using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SigeraAssociados.Infrastructure.Persistence.Entities;

/// <summary>
/// Mapeia integralmente a tabela legada dbo.T000CLIE.
/// Contém os dados específicos do cadastro de associados.
/// </summary>
[Table("T000CLIE", Schema = "dbo")]
[PrimaryKey(nameof(CD_EMP), nameof(SQ_PES_CLI))]
public sealed class T000CLIE
{
	/// <summary>
	/// Código da empresa.
	/// SQL Server: numeric(2,0), NOT NULL.
	/// Integra a chave primária e as chaves estrangeiras.
	/// </summary>
	[Column("CD_EMP", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal CD_EMP { get; set; }

	/// <summary>
	/// Código sequencial da pessoa associada.
	/// SQL Server: numeric(7,0), NOT NULL.
	/// Integra a chave primária composta.
	/// </summary>
	[Column("SQ_PES_CLI", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal SQ_PES_CLI { get; set; }

	/// <summary>
	/// Código da situação do associado.
	/// SQL Server: numeric(2,0), NULL.
	/// </summary>
	[Column("CD_SIT_CLI", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? CD_SIT_CLI { get; set; }

	/// <summary>
	/// Observação sobre a situação do associado.
	/// SQL Server: varchar(100), NULL.
	/// </summary>
	[MaxLength(100)]
	[Column("OB_SIT", TypeName = "varchar(100)")]
	public string? OB_SIT { get; set; }

	/// <summary>
	/// Número da proposta.
	/// SQL Server: decimal(8,0), NULL.
	/// </summary>
	[Column("SQ_PROPOSTA", TypeName = "decimal(8, 0)")]
	[Precision(8, 0)]
	public decimal? SQ_PROPOSTA { get; set; }

	/// <summary>
	/// Código da filial.
	/// SQL Server: numeric(2,0), NULL.
	/// </summary>
	[Column("CD_FIL", TypeName = "numeric(2, 0)")]
	[Precision(2, 0)]
	public decimal? CD_FIL { get; set; }

	/// <summary>
	/// Senha do portal do associado, em texto puro (o legado lê o valor como está).
	/// SQL Server: varchar(50), NULL.
	/// Exposta só no detalhe do associado (AssociadoDados.SenhaPortal); nunca em pesquisa ou log.
	/// </summary>
	[MaxLength(50)]
	[Column("NR_PWS", TypeName = "varchar(50)")]
	public string? NR_PWS { get; set; }

	/// <summary>
	/// Número da carteira EPHAMA.
	/// SQL Server: varchar(50), NULL.
	/// </summary>
	[MaxLength(50)]
	[Column("NR_CARTEIRA_EPHAMA", TypeName = "varchar(50)")]
	public string? NR_CARTEIRA_EPHAMA { get; set; }

	/// <summary>
	/// Data da carteira.
	/// SQL Server: datetime, NULL.
	/// </summary>
	[Column("DT_CARTEIRA", TypeName = "datetime")]
	public DateTime? DT_CARTEIRA { get; set; }

	/// <summary>
	/// Tipo do último movimento.
	/// SQL Server: numeric(3,0), NULL.
	/// </summary>
	[Column("TP_MOV_ULT", TypeName = "numeric(3, 0)")]
	[Precision(3, 0)]
	public decimal? TP_MOV_ULT { get; set; }

	/// <summary>
	/// Senha de averbação do associado, em texto puro (o legado lê o valor como está).
	/// SQL Server: varchar(50), NULL.
	/// Exposta só no detalhe do associado (AssociadoDados.SenhaAverbacao); nunca em pesquisa ou log.
	/// </summary>
	[MaxLength(50)]
	[Column("NR_PWS_02", TypeName = "varchar(50)")]
	public string? NR_PWS_02 { get; set; }

	/// <summary>
	/// Pessoa correspondente ao cadastro de associado.
	/// Relacionamento composto:
	/// T000CLIE(CD_EMP, SQ_PES_CLI)
	/// -> T000PESS(CD_EMP, SQ_PES).
	/// </summary>
	/// <summary>
	/// Data da exclusão lógica feita pela API. Preenchida, o associado
	/// some da API (o sistema legado não usa a coluna).
	/// SQL Server: datetime, NULL. Coluna criada pelo script
	/// scripts/associados-colunas-api-e-permissoes.sql.
	/// </summary>
	[Column("DT_EXC_API", TypeName = "datetime")]
	public DateTime? DT_EXC_API { get; set; }

	/// <summary>
	/// Usuário (e-mail) que fez a exclusão lógica. SQL Server: varchar(256), NULL.
	/// </summary>
	[MaxLength(256)]
	[Column("NM_USU_EXC_API", TypeName = "varchar(256)")]
	public string? NM_USU_EXC_API { get; set; }

	/// <summary>
	/// Corretor que cadastrou o associado pela API: ele vê e altera o
	/// associado mesmo antes de haver contrato. SQL Server: numeric(7,0), NULL.
	/// </summary>
	[Column("SQ_PES_CRO_CAD", TypeName = "numeric(7, 0)")]
	[Precision(7, 0)]
	public decimal? SQ_PES_CRO_CAD { get; set; }

	[ForeignKey(nameof(CD_EMP) + "," + nameof(SQ_PES_CLI))]
	public T000PESS? Pessoa { get; set; }

	/// <summary>
	/// Empresa à qual o associado pertence.
	/// Relacionamento: T000CLIE.CD_EMP -> T009EMPS.CD_EMP.
	/// </summary>
	[ForeignKey(nameof(CD_EMP))]
	public T009EMPS? Empresa { get; set; }

	/// <summary>
	/// Situação atual do associado no cadastro.
	/// Relacionamento composto:
	/// T000CLIE(CD_EMP, CD_SIT_CLI)
	/// -> T000SCLI(CD_EMP, CD_SIT_CLI).
	/// </summary>
	[ForeignKey(nameof(CD_EMP) + "," + nameof(CD_SIT_CLI))]
	[InverseProperty(nameof(T000SCLI.Associados))]
	public T000SCLI? Situacao { get; set; }
}