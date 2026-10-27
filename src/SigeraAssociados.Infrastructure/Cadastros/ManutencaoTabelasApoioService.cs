using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Associados;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Application.Erros;
using SigeraAssociados.Application.Interfaces.Cadastros;
using SigeraAssociados.Application.Orgaos;
using SigeraAssociados.Infrastructure.Persistence.Context;
using SigeraAssociados.Infrastructure.Persistence.Entities;
using SigeraAssociados.Infrastructure.Persistence.Factories;
using SigeraAssociados.Infrastructure.Persistence.Identity;
using SigeraAssociados.Infrastructure.Systems;

namespace SigeraAssociados.Infrastructure.Cadastros;

/// <summary>
/// Grava nas tabelas de apoio do legado: T000PARA (filiais),
/// T000SCLI (situações), T000TRTA (tratamentos) e T000MUNI (municípios).
/// Código novo por MAX + 1 com UPDLOCK, HOLDLOCK (como no cadastro de
/// associados). Exclusão física, recusada quando o registro está em uso.
/// </summary>
public sealed class ManutencaoTabelasApoioService : IManutencaoTabelasApoioService
{
	private readonly SigeraDbContextFactory _factory;
	private readonly SigeraIdentityDbContext _identityDb;

	public ManutencaoTabelasApoioService(
		SigeraDbContextFactory factory,
		SigeraIdentityDbContext identityDb)
	{
		_factory = factory;
		_identityDb = identityDb;
	}

	// ===================== Filiais (T000PARA) =====================

	/// <inheritdoc />
	public async Task<FilialDados?> ObterFilialAsync(
		int sistema,
		decimal cdFil,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var filial = await db.Filiais
			.AsNoTracking()
			.FirstOrDefaultAsync(f => f.CD_FIL == cdFil, cancellationToken);

		return filial is null ? null : ParaDados(filial);
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<FilialDados>> IncluirFilialAsync(
		int sistema,
		FilialDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Filial(dados, inclusao: true) is string erro)
		{
			return Invalido<FilialDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<FilialDados>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.CdFil ?? (await db.Database
				.SqlQuery<decimal>($"SELECT ISNULL(MAX(cd_fil), 0) + 1 AS [Value] FROM dbo.T000PARA WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}")
				.ToListAsync(cancellationToken))
			.Single();

		if (codigo > 99)
		{
			return Invalido<FilialDados>("Não há código de filial livre (o máximo é 99).");
		}

		if (await db.Filiais.AnyAsync(f => f.CD_EMP == emp && f.CD_FIL == codigo, cancellationToken))
		{
			return Conflito<FilialDados>($"A filial {codigo:0} já existe.");
		}

		var filial = new T000PARA { CD_EMP = emp, CD_FIL = codigo };
		Aplicar(dados, filial);

		db.Filiais.Add(filial);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<FilialDados>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Filial cadastrada.", ParaDados(filial));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<FilialDados>> AlterarFilialAsync(
		int sistema,
		decimal cdFil,
		FilialDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Filial(dados, inclusao: false) is string erro)
		{
			return Invalido<FilialDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var filial = await db.Filiais.FirstOrDefaultAsync(f => f.CD_FIL == cdFil, cancellationToken);

		if (filial is null)
		{
			return NaoEncontrado<FilialDados>("Filial não encontrada.");
		}

		Aplicar(dados, filial);
		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Filial alterada.", ParaDados(filial));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<FilialDados>> ExcluirFilialAsync(
		int sistema,
		decimal cdFil,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var filial = await db.Filiais.FirstOrDefaultAsync(f => f.CD_FIL == cdFil, cancellationToken);

		if (filial is null)
		{
			return NaoEncontrado<FilialDados>("Filial não encontrada.");
		}

		var emUso =
			await db.Associados.AnyAsync(a => a.CD_EMP == filial.CD_EMP && a.CD_FIL == cdFil, cancellationToken) ? "associados"
			: await db.Contratos.AnyAsync(c => c.CD_EMP == filial.CD_EMP && c.CD_FIL == cdFil, cancellationToken) ? "contratos"
			: await db.Corretores.AnyAsync(c => c.CD_EMP == filial.CD_EMP && c.CD_FIL == cdFil, cancellationToken) ? "corretores"
			: await _identityDb.UsuarioAcessos.AnyAsync(a => a.Sistema == sistema && a.CdFil == cdFil, cancellationToken) ? "acessos de usuários da API"
			: null;

		if (emUso is not null)
		{
			return Conflito<FilialDados>($"A filial {cdFil:0} não pode ser excluída: há {emUso} ligados a ela.");
		}

		var dto = ParaDados(filial);
		db.Filiais.Remove(filial);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<FilialDados>(erroGravacao)
			: Sucesso("Filial excluída.", dto);
	}

	// ===================== Situações (T000SCLI) =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> IncluirSituacaoAsync(
		int sistema,
		ItemTabelaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Item(dados, ValidacaoTabelasApoio.TamanhoSituacao, inclusao: true) is string erro)
		{
			return Invalido<ItemTabelaDto>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<ItemTabelaDto>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.Codigo ?? (await db.Database
				.SqlQuery<decimal>($"SELECT ISNULL(MAX(CD_SIT_CLI), 0) + 1 AS [Value] FROM dbo.T000SCLI WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}")
				.ToListAsync(cancellationToken))
			.Single();

		if (codigo > 99)
		{
			return Invalido<ItemTabelaDto>("Não há código de situação livre (o máximo é 99).");
		}

		if (await db.SituacoesAssociados.AnyAsync(s => s.CD_EMP == emp && s.CD_SIT_CLI == codigo, cancellationToken))
		{
			return Conflito<ItemTabelaDto>($"A situação {codigo:0} já existe.");
		}

		var situacao = new T000SCLI { CD_EMP = emp, CD_SIT_CLI = codigo, DE_SIT_CLI = dados.Descricao!.Trim() };
		db.SituacoesAssociados.Add(situacao);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<ItemTabelaDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Situação cadastrada.", Item(situacao.CD_SIT_CLI, situacao.DE_SIT_CLI));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> AlterarSituacaoAsync(
		int sistema,
		decimal codigo,
		ItemTabelaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Item(dados, ValidacaoTabelasApoio.TamanhoSituacao, inclusao: false) is string erro)
		{
			return Invalido<ItemTabelaDto>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var situacao = await db.SituacoesAssociados.FirstOrDefaultAsync(s => s.CD_SIT_CLI == codigo, cancellationToken);

		if (situacao is null)
		{
			return NaoEncontrado<ItemTabelaDto>("Situação não encontrada.");
		}

		situacao.DE_SIT_CLI = dados.Descricao!.Trim();
		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Situação alterada.", Item(situacao.CD_SIT_CLI, situacao.DE_SIT_CLI));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> ExcluirSituacaoAsync(
		int sistema,
		decimal codigo,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		if (codigo == ValidacaoCadastroAssociado.SituacaoFalecido)
		{
			return Conflito<ItemTabelaDto>(
				"A situação 10 (falecido) não pode ser excluída: a trigger TR_CLIE_UPD depende dela.");
		}

		await using var db = _factory.Create(sistema);

		var situacao = await db.SituacoesAssociados.FirstOrDefaultAsync(s => s.CD_SIT_CLI == codigo, cancellationToken);

		if (situacao is null)
		{
			return NaoEncontrado<ItemTabelaDto>("Situação não encontrada.");
		}

		if (await db.Associados.AnyAsync(a => a.CD_EMP == situacao.CD_EMP && a.CD_SIT_CLI == codigo, cancellationToken))
		{
			return Conflito<ItemTabelaDto>($"A situação {codigo:0} não pode ser excluída: há associados nela.");
		}

		var dto = Item(situacao.CD_SIT_CLI, situacao.DE_SIT_CLI);
		db.SituacoesAssociados.Remove(situacao);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<ItemTabelaDto>(erroGravacao)
			: Sucesso("Situação excluída.", dto);
	}

	// ===================== Tratamentos (T000TRTA) =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> IncluirTratamentoAsync(
		int sistema,
		ItemTabelaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Item(dados, ValidacaoTabelasApoio.TamanhoTratamento, inclusao: true) is string erro)
		{
			return Invalido<ItemTabelaDto>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<ItemTabelaDto>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.Codigo ?? (await db.Database
				.SqlQuery<decimal>($"SELECT ISNULL(MAX(CD_TRT), 0) + 1 AS [Value] FROM dbo.T000TRTA WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}")
				.ToListAsync(cancellationToken))
			.Single();

		if (codigo > 99)
		{
			return Invalido<ItemTabelaDto>("Não há código de tratamento livre (o máximo é 99).");
		}

		if (await db.Tratamentos.AnyAsync(t => t.CD_EMP == emp && t.CD_TRT == codigo, cancellationToken))
		{
			return Conflito<ItemTabelaDto>($"O tratamento {codigo:0} já existe.");
		}

		var tratamento = new T000TRTA { CD_EMP = emp, CD_TRT = codigo, DE_TRT = dados.Descricao!.Trim() };
		db.Tratamentos.Add(tratamento);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<ItemTabelaDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Tratamento cadastrado.", Item(tratamento.CD_TRT, tratamento.DE_TRT));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> AlterarTratamentoAsync(
		int sistema,
		decimal codigo,
		ItemTabelaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Item(dados, ValidacaoTabelasApoio.TamanhoTratamento, inclusao: false) is string erro)
		{
			return Invalido<ItemTabelaDto>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var tratamento = await db.Tratamentos.FirstOrDefaultAsync(t => t.CD_TRT == codigo, cancellationToken);

		if (tratamento is null)
		{
			return NaoEncontrado<ItemTabelaDto>("Tratamento não encontrado.");
		}

		tratamento.DE_TRT = dados.Descricao!.Trim();
		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Tratamento alterado.", Item(tratamento.CD_TRT, tratamento.DE_TRT));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> ExcluirTratamentoAsync(
		int sistema,
		decimal codigo,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var tratamento = await db.Tratamentos.FirstOrDefaultAsync(t => t.CD_TRT == codigo, cancellationToken);

		if (tratamento is null)
		{
			return NaoEncontrado<ItemTabelaDto>("Tratamento não encontrado.");
		}

		if (await db.PessoasFisicas.AnyAsync(f => f.CD_EMP == tratamento.CD_EMP && f.CD_TRT == codigo, cancellationToken))
		{
			return Conflito<ItemTabelaDto>($"O tratamento {codigo:0} não pode ser excluído: há pessoas com ele.");
		}

		var dto = Item(tratamento.CD_TRT, tratamento.DE_TRT);
		db.Tratamentos.Remove(tratamento);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<ItemTabelaDto>(erroGravacao)
			: Sucesso("Tratamento excluído.", dto);
	}

	// ===================== Bancos (T000BANC) =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<BancoDados>> IncluirBancoAsync(
		int sistema,
		BancoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Banco(dados, inclusao: true) is string erro)
		{
			return Invalido<BancoDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<BancoDados>();
		}

		var codigo = dados.CdBanco!.Value;

		if (await db.Bancos.AnyAsync(b => b.CD_EMP == emp && b.CD_BCO == codigo, cancellationToken))
		{
			return Conflito<BancoDados>($"O banco {codigo:0} já existe.");
		}

		var banco = new T000BANC
		{
			CD_EMP = emp,
			CD_BCO = codigo,
			NM_BCO = dados.Nome!.Trim(),
			CD_SIT_BCO = dados.CdSituacao ?? SituacaoAtivoInativo.Banco.Ativo
		};

		db.Bancos.Add(banco);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<BancoDados>(erroGravacao)
			: Sucesso("Banco cadastrado.", ParaDados(banco));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<BancoDados>> AlterarBancoAsync(
		int sistema,
		decimal cdBanco,
		BancoDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Banco(dados, inclusao: false) is string erro)
		{
			return Invalido<BancoDados>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var banco = await db.Bancos.FirstOrDefaultAsync(b => b.CD_BCO == cdBanco, cancellationToken);

		if (banco is null)
		{
			return NaoEncontrado<BancoDados>("Banco não encontrado.");
		}

		banco.NM_BCO = dados.Nome!.Trim();
		banco.CD_SIT_BCO = dados.CdSituacao;
		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Banco alterado.", ParaDados(banco));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<BancoDados>> ExcluirBancoAsync(
		int sistema,
		decimal cdBanco,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var banco = await db.Bancos.FirstOrDefaultAsync(b => b.CD_BCO == cdBanco, cancellationToken);

		if (banco is null)
		{
			return NaoEncontrado<BancoDados>("Banco não encontrado.");
		}

		if (await db.ContasBancarias.AnyAsync(c => c.CD_EMP == banco.CD_EMP && c.CD_BCO == cdBanco, cancellationToken))
		{
			return Conflito<BancoDados>($"O banco {cdBanco:0} não pode ser excluído: há contas nele.");
		}

		var dto = ParaDados(banco);
		db.Bancos.Remove(banco);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<BancoDados>(erroGravacao)
			: Sucesso("Banco excluído.", dto);
	}

	private static BancoDados ParaDados(T000BANC b) => new()
	{
		CdBanco = b.CD_BCO,
		Nome = b.NM_BCO,
		CdSituacao = b.CD_SIT_BCO,
		Situacao = SituacaoAtivoInativo.Banco.Descricao(b.CD_SIT_BCO)
	};

	// ===================== Tipos de endereço (T000TEND) =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> IncluirTipoEnderecoAsync(
		int sistema,
		ItemTabelaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Item(dados, ValidacaoTabelasApoio.TamanhoTipoEndereco, inclusao: true) is string erro)
		{
			return Invalido<ItemTabelaDto>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<ItemTabelaDto>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.Codigo ?? (await db.Database
				.SqlQuery<decimal>($"SELECT ISNULL(MAX(CD_TP_EN), 0) + 1 AS [Value] FROM dbo.T000TEND WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}")
				.ToListAsync(cancellationToken))
			.Single();

		if (codigo > 99)
		{
			return Invalido<ItemTabelaDto>("Não há código de tipo de endereço livre (o máximo é 99).");
		}

		if (await db.TiposEndereco.AnyAsync(t => t.CD_EMP == emp && t.CD_TP_EN == codigo, cancellationToken))
		{
			return Conflito<ItemTabelaDto>($"O tipo de endereço {codigo:0} já existe.");
		}

		var tipo = new T000TEND { CD_EMP = emp, CD_TP_EN = codigo, DE_TP_EN = dados.Descricao!.Trim() };
		db.TiposEndereco.Add(tipo);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<ItemTabelaDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Tipo de endereço cadastrado.", Item(tipo.CD_TP_EN, tipo.DE_TP_EN));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> AlterarTipoEnderecoAsync(
		int sistema,
		decimal codigo,
		ItemTabelaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Item(dados, ValidacaoTabelasApoio.TamanhoTipoEndereco, inclusao: false) is string erro)
		{
			return Invalido<ItemTabelaDto>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var tipo = await db.TiposEndereco.FirstOrDefaultAsync(t => t.CD_TP_EN == codigo, cancellationToken);

		if (tipo is null)
		{
			return NaoEncontrado<ItemTabelaDto>("Tipo de endereço não encontrado.");
		}

		tipo.DE_TP_EN = dados.Descricao!.Trim();
		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Tipo de endereço alterado.", Item(tipo.CD_TP_EN, tipo.DE_TP_EN));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> ExcluirTipoEnderecoAsync(
		int sistema,
		decimal codigo,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var tipo = await db.TiposEndereco.FirstOrDefaultAsync(t => t.CD_TP_EN == codigo, cancellationToken);

		if (tipo is null)
		{
			return NaoEncontrado<ItemTabelaDto>("Tipo de endereço não encontrado.");
		}

		if (await db.Enderecos.AnyAsync(e => e.CD_EMP == tipo.CD_EMP && e.CD_TP_EN == codigo, cancellationToken))
		{
			return Conflito<ItemTabelaDto>($"O tipo de endereço {codigo:0} não pode ser excluído: há endereços com ele.");
		}

		var dto = Item(tipo.CD_TP_EN, tipo.DE_TP_EN);
		db.TiposEndereco.Remove(tipo);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<ItemTabelaDto>(erroGravacao)
			: Sucesso("Tipo de endereço excluído.", dto);
	}

	// ===================== Tipos de telefone (T000TTEL) =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> IncluirTipoTelefoneAsync(
		int sistema,
		ItemTabelaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Item(dados, ValidacaoTabelasApoio.TamanhoTipoTelefone, inclusao: true) is string erro)
		{
			return Invalido<ItemTabelaDto>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<ItemTabelaDto>();
		}

		await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

		var codigo = dados.Codigo ?? (await db.Database
				.SqlQuery<decimal>($"SELECT ISNULL(MAX(CD_TP_FN), 0) + 1 AS [Value] FROM dbo.T000TTEL WITH (UPDLOCK, HOLDLOCK) WHERE CD_EMP = {emp}")
				.ToListAsync(cancellationToken))
			.Single();

		if (codigo > 99)
		{
			return Invalido<ItemTabelaDto>("Não há código de tipo de telefone livre (o máximo é 99).");
		}

		if (await db.TiposTelefone.AnyAsync(t => t.CD_EMP == emp && t.CD_TP_FN == codigo, cancellationToken))
		{
			return Conflito<ItemTabelaDto>($"O tipo de telefone {codigo:0} já existe.");
		}

		var tipo = new T000TTEL { CD_EMP = emp, CD_TP_FN = codigo, DE_TP_FN = dados.Descricao!.Trim() };
		db.TiposTelefone.Add(tipo);

		if (await SalvarAsync(db, cancellationToken) is string erroGravacao)
		{
			return Conflito<ItemTabelaDto>(erroGravacao);
		}

		await transacao.CommitAsync(cancellationToken);

		return Sucesso("Tipo de telefone cadastrado.", Item(tipo.CD_TP_FN, tipo.DE_TP_FN));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> AlterarTipoTelefoneAsync(
		int sistema,
		decimal codigo,
		ItemTabelaDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Item(dados, ValidacaoTabelasApoio.TamanhoTipoTelefone, inclusao: false) is string erro)
		{
			return Invalido<ItemTabelaDto>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var tipo = await db.TiposTelefone.FirstOrDefaultAsync(t => t.CD_TP_FN == codigo, cancellationToken);

		if (tipo is null)
		{
			return NaoEncontrado<ItemTabelaDto>("Tipo de telefone não encontrado.");
		}

		tipo.DE_TP_FN = dados.Descricao!.Trim();
		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Tipo de telefone alterado.", Item(tipo.CD_TP_FN, tipo.DE_TP_FN));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<ItemTabelaDto>> ExcluirTipoTelefoneAsync(
		int sistema,
		decimal codigo,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var tipo = await db.TiposTelefone.FirstOrDefaultAsync(t => t.CD_TP_FN == codigo, cancellationToken);

		if (tipo is null)
		{
			return NaoEncontrado<ItemTabelaDto>("Tipo de telefone não encontrado.");
		}

		if (await db.Telefones.AnyAsync(e => e.CD_EMP == tipo.CD_EMP && e.CD_TP_FN == codigo, cancellationToken))
		{
			return Conflito<ItemTabelaDto>($"O tipo de telefone {codigo:0} não pode ser excluído: há telefones com ele.");
		}

		var dto = Item(tipo.CD_TP_FN, tipo.DE_TP_FN);
		db.TiposTelefone.Remove(tipo);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<ItemTabelaDto>(erroGravacao)
			: Sucesso("Tipo de telefone excluído.", dto);
	}

	// ===================== Municípios (T000MUNI) =====================

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<MunicipioDto>> IncluirMunicipioAsync(
		int sistema,
		MunicipioDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Municipio(dados, inclusao: true) is string erro)
		{
			return Invalido<MunicipioDto>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		if (await ObterEmpresaAsync(db, cancellationToken) is not decimal emp)
		{
			return EmpresaIndefinida<MunicipioDto>();
		}

		var codigo = dados.CdMun!.Value;

		if (await db.Municipios.AnyAsync(m => m.CD_EMP == emp && m.CD_MUN == codigo, cancellationToken))
		{
			return Conflito<MunicipioDto>($"O município {codigo:0} já existe.");
		}

		var municipio = new T000MUNI
		{
			CD_EMP = emp,
			CD_MUN = codigo,
			DE_MUN = dados.Nome!.Trim(),
			SG_UF = dados.Uf!.Trim().ToUpperInvariant()
		};

		db.Municipios.Add(municipio);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<MunicipioDto>(erroGravacao)
			: Sucesso("Município cadastrado.", ParaDto(municipio));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<MunicipioDto>> AlterarMunicipioAsync(
		int sistema,
		decimal cdMun,
		MunicipioDados dados,
		CancellationToken cancellationToken = default)
	{
		if (ValidacaoTabelasApoio.Municipio(dados, inclusao: false) is string erro)
		{
			return Invalido<MunicipioDto>(erro);
		}

		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var municipio = await db.Municipios.FirstOrDefaultAsync(m => m.CD_MUN == cdMun, cancellationToken);

		if (municipio is null)
		{
			return NaoEncontrado<MunicipioDto>("Município não encontrado.");
		}

		municipio.DE_MUN = dados.Nome!.Trim();
		municipio.SG_UF = dados.Uf!.Trim().ToUpperInvariant();
		await db.SaveChangesAsync(cancellationToken);

		return Sucesso("Município alterado.", ParaDto(municipio));
	}

	/// <inheritdoc />
	public async Task<ResultadoTabelaApoio<MunicipioDto>> ExcluirMunicipioAsync(
		int sistema,
		decimal cdMun,
		CancellationToken cancellationToken = default)
	{
		ValidarSistema(sistema);

		await using var db = _factory.Create(sistema);

		var municipio = await db.Municipios.FirstOrDefaultAsync(m => m.CD_MUN == cdMun, cancellationToken);

		if (municipio is null)
		{
			return NaoEncontrado<MunicipioDto>("Município não encontrado.");
		}

		var emUso =
			await db.PessoasFisicas.AnyAsync(f => f.CD_EMP == municipio.CD_EMP && f.CD_MUN == cdMun, cancellationToken) ? "pessoas"
			: await db.Enderecos.AnyAsync(e => e.CD_EMP == municipio.CD_EMP && e.CD_MUN == cdMun, cancellationToken) ? "endereços"
			: await db.Corretores.AnyAsync(c => c.CD_EMP == municipio.CD_EMP && c.CD_MUN == cdMun, cancellationToken) ? "corretores"
			: null;

		if (emUso is not null)
		{
			return Conflito<MunicipioDto>($"O município {cdMun:0} não pode ser excluído: há {emUso} ligados a ele.");
		}

		var dto = ParaDto(municipio);
		db.Municipios.Remove(municipio);

		return await SalvarAsync(db, cancellationToken) is string erroGravacao
			? Conflito<MunicipioDto>(erroGravacao)
			: Sucesso("Município excluído.", dto);
	}

	// ===================== Apoio =====================

	/// <summary>
	/// Grava e traduz as recusas do banco: chave repetida e registro
	/// usado por outra tabela (FK). Devolve a mensagem ou null.
	/// </summary>
	private static async Task<string?> SalvarAsync(SigeraDbContext db, CancellationToken cancellationToken)
	{
		try
		{
			await db.SaveChangesAsync(cancellationToken);
			return null;
		}
		catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 or 2601 })
		{
			return "Já existe um registro com este código.";
		}
		catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
		{
			return "O registro é usado por outra tabela do sistema e não pode ser excluído.";
		}
	}

	private static void Aplicar(FilialDados dados, T000PARA filial)
	{
		filial.NM_EMP = dados.Nome!.Trim();
		filial.NC_EMP = Texto(dados.NomeCurto);
		filial.NM_LOG_EMP = Texto(dados.Logradouro);
		filial.NR_LOG_EMP = dados.Numero;
		filial.CP_EN_EMP = Texto(dados.Complemento);
		filial.NM_BAI_EMP = Texto(dados.Bairro);
		filial.NR_CEP_EMP = Numero(dados.Cep);
		filial.DE_MUN_EMP = Texto(dados.Municipio);
		filial.SG_UF_EMP = Texto(dados.Uf)?.ToUpperInvariant();
		filial.NR_DDD_FN = dados.Ddd;
		filial.NR_FN = Texto(dados.Telefone);
		filial.NR_DDD_FAX = dados.DddFax;
		filial.NR_FAX = Texto(dados.Fax);
		filial.NR_CPJ = Numero(dados.Cnpj);
	}

	private static FilialDados ParaDados(T000PARA f) => new()
	{
		CdFil = f.CD_FIL,
		Nome = f.NM_EMP,
		NomeCurto = f.NC_EMP,
		Logradouro = f.NM_LOG_EMP,
		Numero = f.NR_LOG_EMP,
		Complemento = f.CP_EN_EMP,
		Bairro = f.NM_BAI_EMP,
		Cep = f.NR_CEP_EMP?.ToString("00000000", CultureInfo.InvariantCulture),
		Municipio = f.DE_MUN_EMP,
		Uf = f.SG_UF_EMP,
		Ddd = f.NR_DDD_FN,
		Telefone = f.NR_FN,
		DddFax = f.NR_DDD_FAX,
		Fax = f.NR_FAX,
		Cnpj = f.NR_CPJ?.ToString("00000000000000", CultureInfo.InvariantCulture)
	};

	private static MunicipioDto ParaDto(T000MUNI m) => new() { CdMun = m.CD_MUN, Nome = m.DE_MUN, Uf = m.SG_UF };

	private static ItemTabelaDto Item(decimal codigo, string descricao) => new() { Codigo = codigo, Descricao = descricao };

	private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

	private static decimal? Numero(string? valor) =>
		ValidacaoTabelasApoio.SomenteDigitos(valor) is { Length: > 0 } digitos
			? decimal.Parse(digitos, CultureInfo.InvariantCulture)
			: null;

	private static async Task<decimal?> ObterEmpresaAsync(SigeraDbContext db, CancellationToken cancellationToken)
	{
		var empresas = await db.Empresas.AsNoTracking().Select(e => e.CD_EMP).Take(2).ToListAsync(cancellationToken);

		return empresas.Count == 1 ? empresas[0] : null;
	}

	private static void ValidarSistema(int sistema)
	{
		if (!SigeraSystemCatalog.TryGetDatabase(sistema, out _))
		{
			throw new DadosInvalidosException(
				"Sistema inválido. Os valores permitidos são 1, 2, 3 e 4.");
		}
	}

	private static ResultadoTabelaApoio<T> Sucesso<T>(string mensagem, T item) => new(StatusTabelaApoio.Sucesso, mensagem, item);

	private static ResultadoTabelaApoio<T> Invalido<T>(string mensagem) => new(StatusTabelaApoio.Invalido, mensagem);

	private static ResultadoTabelaApoio<T> Conflito<T>(string mensagem) => new(StatusTabelaApoio.Conflito, mensagem);

	private static ResultadoTabelaApoio<T> NaoEncontrado<T>(string mensagem) => new(StatusTabelaApoio.NaoEncontrado, mensagem);

	private static ResultadoTabelaApoio<T> EmpresaIndefinida<T>() =>
		new(StatusTabelaApoio.Falha,
			"Não foi possível identificar a empresa do sistema: a tabela T009EMPS deve ter exatamente uma empresa.");
}
