using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SigeraAssociados.Application.Usuarios;

namespace SigeraAssociados.Api;

/// <summary>
/// Realiza o provisionamento inicial dos grupos funcionais
/// e da conta administrativa.
/// Este processo não é disponibilizado por um endpoint HTTP.
/// </summary>
public static class SeedData
{
	/// <summary>
	/// Provisiona os grupos funcionais e a conta administrativa inicial.
	/// A senha deve ser fornecida por um provedor seguro de configuração.
	/// </summary>
	public static async Task InicializarAsync(
		IServiceProvider serviceProvider,
		IConfiguration configuration,
		bool ambienteDesenvolvimento)
	{
		var logger = serviceProvider
			.GetRequiredService<ILoggerFactory>()
			.CreateLogger("SeedData");

		// O provisionamento precisa estar explicitamente habilitado.
		var habilitado = configuration.GetValue<bool>(
			"SeedAdmin:Habilitado");

		if (!habilitado)
		{
			logger.LogInformation(
				"Provisionamento inicial desabilitado.");

			return;
		}

		// Impede a execução deste provisionamento em produção.
		if (!ambienteDesenvolvimento)
		{
			throw new InvalidOperationException(
				"O provisionamento inicial só pode ser executado " +
				"no ambiente Development.");
		}

		var email = configuration["SeedAdmin:Email"]?.Trim();
		var senha = configuration["SeedAdmin:Senha"];

		if (string.IsNullOrWhiteSpace(email))
		{
			throw new InvalidOperationException(
				"Configure SeedAdmin:Email nos User Secrets.");
		}

		if (string.IsNullOrWhiteSpace(senha))
		{
			throw new InvalidOperationException(
				"Configure SeedAdmin:Senha nos User Secrets.");
		}

		var userManager =
			serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

		var roleManager =
			serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

		// Mantém a função antiga enquanto a autorização é migrada.
		var nomesGrupos = Grupos.Funcionais
			.Append(Grupos.AdministradorLegado)
			.Distinct(StringComparer.OrdinalIgnoreCase);

		// Cria os grupos que ainda não existem.
		foreach (var nomeGrupo in nomesGrupos)
		{
			if (await roleManager.RoleExistsAsync(nomeGrupo))
			{
				continue;
			}

			var resultadoGrupo = await roleManager.CreateAsync(
				new IdentityRole(nomeGrupo));

			if (!resultadoGrupo.Succeeded)
			{
				var codigosErro = string.Join(
					"; ",
					resultadoGrupo.Errors.Select(erro => erro.Code));

				throw new InvalidOperationException(
					$"Não foi possível criar o grupo '{nomeGrupo}': " +
					codigosErro);
			}

			logger.LogInformation(
				"Grupo funcional criado: {Grupo}.",
				nomeGrupo);
		}

		// Localiza a conta administrativa configurada.
		var usuario = await userManager.FindByEmailAsync(email);

		if (usuario is null)
		{
			// Cria a conta apenas quando ela ainda não existe.
			usuario = new IdentityUser
			{
				UserName = email,
				Email = email,
				EmailConfirmed = true
			};

			var resultadoUsuario = await userManager.CreateAsync(
				usuario,
				senha);

			if (!resultadoUsuario.Succeeded)
			{
				var codigosErro = string.Join(
					"; ",
					resultadoUsuario.Errors.Select(erro => erro.Code));

				throw new InvalidOperationException(
					"Não foi possível criar o usuário administrativo: " +
					codigosErro);
			}

			logger.LogInformation(
				"Usuário administrativo inicial criado.");
		}
		else
		{
			// Preserva a senha, o e-mail e o estado de confirmação existentes.
			logger.LogInformation(
				"Conta administrativa existente localizada. " +
				"A senha e os dados pessoais serão preservados.");
		}

		// Concede a função legada e o novo grupo administrativo
		// exclusivamente à conta configurada para o provisionamento.
		var funcoesAtuais = await userManager.GetRolesAsync(usuario);

		var funcoesAdministrativas = new[]
		{
			Grupos.AdministradorLegado,
			Grupos.Administradores
		};

		foreach (var nomeFuncao in funcoesAdministrativas)
		{
			if (funcoesAtuais.Contains(
				nomeFuncao,
				StringComparer.OrdinalIgnoreCase))
			{
				continue;
			}

			var resultadoAtribuicao = await userManager.AddToRoleAsync(
				usuario,
				nomeFuncao);

			if (!resultadoAtribuicao.Succeeded)
			{
				var codigosErro = string.Join(
					"; ",
					resultadoAtribuicao.Errors.Select(erro => erro.Code));

				throw new InvalidOperationException(
					$"Não foi possível atribuir a função '{nomeFuncao}': " +
					codigosErro);
			}

			logger.LogInformation(
				"Função {Funcao} atribuída à conta administrativa.",
				nomeFuncao);
		}

		logger.LogInformation(
			"Provisionamento inicial concluído.");
	}
}
