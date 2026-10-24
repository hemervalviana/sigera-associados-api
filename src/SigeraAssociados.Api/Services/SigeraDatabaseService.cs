
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Api.Services;

public sealed class SigeraDatabaseService
{
	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraSystemContext _systemContext;

	public SigeraDatabaseService(
		SigeraDbContextFactory factory,
		SigeraSystemContext systemContext)
	{
		_factory = factory;
		_systemContext = systemContext;
	}

	public SigeraDbContext CreateDbContext()
	{
		if (!_systemContext.SystemCode.HasValue)
		{
			throw new InvalidOperationException(
				"O sistema SIGERA não foi selecionado para esta requisição.");
		}

		return _factory.Create(_systemContext.SystemCode.Value);
	}
}


