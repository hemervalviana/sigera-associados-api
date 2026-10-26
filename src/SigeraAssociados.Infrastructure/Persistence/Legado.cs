using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SigeraAssociados.Application.Cadastros;
using SigeraAssociados.Infrastructure.Persistence.Context;

namespace SigeraAssociados.Infrastructure.Persistence;

/// <summary>
/// Apoio comum às gravações nas bases do legado.
/// </summary>
internal static class Legado
{
	/// <summary>
	/// Empresa da base (T009EMPS). Null quando não há exatamente uma.
	/// </summary>
	public static async Task<decimal?> ObterEmpresaAsync(SigeraDbContext db, CancellationToken cancellationToken)
	{
		var empresas = await db.Empresas.AsNoTracking().Select(e => e.CD_EMP).Take(2).ToListAsync(cancellationToken);

		return empresas.Count == 1 ? empresas[0] : null;
	}

	/// <summary>
	/// Executa um SELECT ... AS [Value] que devolve um número (MAX + 1).
	/// </summary>
	public static async Task<decimal> NumeroAsync(SigeraDbContext db, FormattableString sql, CancellationToken cancellationToken)
	{
		return (await db.Database.SqlQuery<decimal>(sql).ToListAsync(cancellationToken)).Single();
	}

	/// <summary>
	/// Grava e traduz as recusas do banco. Devolve a mensagem ou null.
	/// </summary>
	public static async Task<string?> SalvarAsync(SigeraDbContext db, CancellationToken cancellationToken)
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
		catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 } sql)
		{
			return sql.Message.Contains("DELETE", StringComparison.OrdinalIgnoreCase)
				? "O registro é usado por outra tabela do sistema e não pode ser excluído."
				: "Um dos códigos informados não existe na tabela de referência do sistema.";
		}
	}

	public static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

	public static decimal? Numero(string? valor) =>
		ValidacaoTabelasApoio.SomenteDigitos(valor) is { Length: > 0 } digitos
			? decimal.Parse(digitos, CultureInfo.InvariantCulture)
			: null;

	public static string? Digitos(decimal? valor, int tamanho) =>
		valor?.ToString(new string('0', tamanho), CultureInfo.InvariantCulture);

	public static string? Digitos(decimal? valor) =>
		valor?.ToString("0", CultureInfo.InvariantCulture);

	public static ResultadoTabelaApoio<T> Sucesso<T>(string mensagem, T item) => new(StatusTabelaApoio.Sucesso, mensagem, item);

	public static ResultadoTabelaApoio<T> Invalido<T>(string mensagem) => new(StatusTabelaApoio.Invalido, mensagem);

	public static ResultadoTabelaApoio<T> Conflito<T>(string mensagem) => new(StatusTabelaApoio.Conflito, mensagem);

	public static ResultadoTabelaApoio<T> NaoEncontrado<T>(string mensagem) => new(StatusTabelaApoio.NaoEncontrado, mensagem);

	public static ResultadoTabelaApoio<T> EmpresaIndefinida<T>() =>
		new(StatusTabelaApoio.Falha,
			"Não foi possível identificar a empresa do sistema: a tabela T009EMPS deve ter exatamente uma empresa.");
}
