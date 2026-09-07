using Credito.Aplicacao.Consultas;

namespace Credito.Api.Contratos;

internal static class EndpointsDeContratosDaConta
{
    /// <summary>
    /// Os emprestimos de uma conta bancaria.
    /// </summary>
    /// <remarks>
    /// Fora do grupo <c>/propostas/{id}/contrato</c> de proposito: aqui a pergunta parte da
    /// conta, e nao da proposta. E a rota que a area de emprestimo do banco abre — sem ela,
    /// mostrar os contratos de alguem exigiria ja saber quais propostas sao dessa pessoa.
    /// </remarks>
    public static void MapearContratosDaConta(this IEndpointRouteBuilder rotas) =>
        rotas.MapGet("/contratos", Listar).WithTags("Contratos");

    private static async Task<IResult> Listar(
        Guid? contaId,
        ListarContratosDaConta listar,
        CancellationToken cancelamento)
    {
        // Sem conta nao ha o que listar, e devolver a carteira inteira seria vazar contrato
        // de todo mundo numa rota que ninguem autentica.
        if (contaId is not { } conta)
        {
            return Results.Problem(
                title: "Parametro contaId obrigatorio",
                detail: "Informe a conta cujos contratos voce quer ver.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(await listar.Executar(conta, cancelamento).ConfigureAwait(false));
    }
}
