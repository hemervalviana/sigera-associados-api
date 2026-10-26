using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SigeraAssociados.Infrastructure.Persistence.Entities;

namespace SigeraAssociados.Infrastructure.Persistence.Configurations;

/// <summary>
/// T000CLIE tem a trigger TR_CLIE_UPD (UPDATE). Sem esta marcação o
/// Entity Framework grava com OUTPUT, que o SQL Server recusa em tabela
/// com trigger.
/// </summary>
public sealed class T000CLIEConfiguration : IEntityTypeConfiguration<T000CLIE>
{
	public void Configure(EntityTypeBuilder<T000CLIE> builder)
	{
		builder.ToTable(tabela => tabela.HasTrigger("TR_CLIE_UPD"));
	}
}
