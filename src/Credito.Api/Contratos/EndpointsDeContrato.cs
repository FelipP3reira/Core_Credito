using Credito.Aplicacao.Contratos;
using Microsoft.AspNetCore.Mvc;

namespace Credito.Api.Contratos;

/// <param name="PrimeiroVencimento">
/// Opcional. Sem ele, a primeira parcela vence trinta dias depois da contratacao.
/// </param>
public sealed record PedidoDeContratacao(DateOnly? PrimeiroVencimento);

internal static class EndpointsDeContrato
{
    private const string CabecalhoDeIdempotencia = "Idempotency-Key";
    private const int TamanhoMaximoDaChave = 64;

    public static void MapearContratos(this IEndpointRouteBuilder rotas)
    {
        var contrato = rotas.MapGroup("/propostas/{id:guid}/contrato").WithTags("Contratos");

        contrato.MapPost("/", Contratar);
        contrato.MapGet("/", Detalhar);
        contrato.MapPost("/parcelas/{numero:int}/pagamento", Pagar);
    }

    private static async Task<IResult> Contratar(
        Guid id,
        PedidoDeContratacao? pedido,
        ContratarProposta contratar,
        CancellationToken cancelamento)
    {
        var detalhe = await contratar
            .Executar(id, pedido?.PrimeiroVencimento, cancelamento)
            .ConfigureAwait(false);

        return Results.Created($"/propostas/{id}/contrato", detalhe);
    }

    private static async Task<IResult> Detalhar(
        Guid id,
        ConsultarContrato consultar,
        CancellationToken cancelamento) =>
        Results.Ok(await consultar.Executar(id, cancelamento).ConfigureAwait(false));

    /// <remarks>
    /// A chave de idempotencia aqui nao e conveniencia: pagamento reenviado por tempo
    /// esgotado precisa ser reconhecido como o mesmo pagamento, e nao como uma segunda
    /// tentativa de quitar a parcela.
    /// </remarks>
    private static async Task<IResult> Pagar(
        Guid id,
        int numero,
        [FromHeader(Name = CabecalhoDeIdempotencia)] string? chaveDoPagamento,
        RegistrarPagamento registrar,
        CancellationToken cancelamento)
    {
        if (string.IsNullOrWhiteSpace(chaveDoPagamento) || chaveDoPagamento.Length > TamanhoMaximoDaChave)
        {
            return Results.Problem(
                title: "Chave de idempotencia ausente ou longa demais",
                detail: $"Envie o cabecalho {CabecalhoDeIdempotencia} com ate {TamanhoMaximoDaChave} caracteres.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(
            await registrar.Executar(id, numero, chaveDoPagamento, cancelamento).ConfigureAwait(false));
    }
}
