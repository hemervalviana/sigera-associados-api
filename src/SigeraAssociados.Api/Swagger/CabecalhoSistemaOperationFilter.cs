using Microsoft.OpenApi;
using SigeraAssociados.Api.Middlewares;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SigeraAssociados.Api.Swagger;

/// <summary>
/// Adiciona o cabeçalho obrigatório X-Sigera-Sistema às operações
/// das rotas de negócio, para que ele apareça como campo no Swagger.
/// Usa a mesma regra de rotas do SigeraSystemMiddleware.
/// </summary>
public sealed class CabecalhoSistemaOperationFilter : IOperationFilter
{
	public void Apply(OpenApiOperation operation, OperationFilterContext context)
	{
		var caminho = "/" + (context.ApiDescription.RelativePath ?? string.Empty)
			.Split('?')[0];

		if (!SigeraSystemMiddleware.RotaDeNegocio(new PathString(caminho)))
		{
			return;
		}

		operation.Parameters ??= new List<IOpenApiParameter>();

		operation.Parameters.Add(new OpenApiParameter
		{
			Name = SigeraSystemMiddleware.HeaderName,
			In = ParameterLocation.Header,
			Required = true,
			Description = "Sistema: 1 = PROASP, 2 = SONASP, 3 = ABSP, 4 = GSP.",
			Schema = new OpenApiSchema
			{
				Type = JsonSchemaType.Integer,
				Format = "int32"
			}
		});
	}
}
