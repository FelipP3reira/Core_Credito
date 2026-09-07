using Credito.Aplicacao.Contratos;
using Microsoft.AspNetCore.Mvc;

namespace Credito.Api.Contratos;

/// <param name="PrimeiroVencimento">
/// Opcional. Sem ele, a primeira parcela vence trinta dias depois da contratacao.
/// </param>
/// <param name="ContaId">
/// A conta da Plataforma Bancaria que recebe o desembolso e paga as parcelas. Opcional:
/// sem ela o contrato existe do mesmo jeito e a liquidacao acontece por fora.
/// </param>
public sealed record PedidoDeContratacao(DateOnly? PrimeiroVencimento, Guid? ContaId);

internal static class EndpointsDeContrato
{
    private const string CabecalhoDeIdempotencia = "Idempotency-Key";
    private const int TamanhoMaximoDaChave = 64;

    public static void MapearContratos(this IEndpointRouteBuilder rotas)
    {
        var contrato = rotas.MapGroup("/propostas/{id:guid}/contrato").WithTags("Contratos");

        contrato.MapPost("/", Contratar);
        contrato.MapGet("/", Detalhar);
        contrato.MapPost("/desembolso", Desembolsar);
        contrato.MapPost("/parcelas/{numero:int}/pagamento", Pagar);
    }

    private static async Task<IResult> Contratar(
        Guid id,
        PedidoDeContratacao? pedido,
        ContratarProposta contratar,
        CancellationToken cancelamento)
    {
        var detalhe = await contratar
            .Executar(id, pedido?.PrimeiroVencimento, pedido?.ContaId, cancelamento)
            .ConfigureAwait(false);

        return Results.Created($"/propostas/{id}/contrato", detalhe);
    }

    private static async Task<IResult> Detalhar(
        Guid id,
        ConsultarContrato consultar,
        CancellationToken cancelamento) =>
        Results.Ok(await consultar.Executar(id, cancelamento).ConfigureAwait(false));

    /// <summary>
    /// Poe o valor financiado na conta do cliente.
    /// </summary>
    /// <remarks>
    /// Rota separada da contratacao porque sao dois sistemas: assinar grava aqui, creditar
    /// grava no banco, e nao existe transacao cobrindo os dois. Sendo um pedido a parte,
    /// ele pode ser repetido ate dar certo — a chave e derivada do contrato, entao repetir
    /// nunca credita duas vezes.
    /// <para>
    /// Sem cabecalho de idempotencia: a chave nao vem do cliente justamente porque ela
    /// precisa ser sempre a mesma para o mesmo contrato. Chave escolhida por quem chama
    /// permitiria dois desembolsos com chaves diferentes.
    /// </para>
    /// </remarks>
    private static async Task<IResult> Desembolsar(
        Guid id,
        DesembolsarContrato desembolsar,
        CancellationToken cancelamento)
    {
        var desembolso = await desembolsar.Executar(id, cancelamento).ConfigureAwait(false);

        // 201 na primeira vez e 200 quando o banco reconheceu a chave: quem chama distingue
        // "acabou de entrar" de "ja tinha entrado" sem comparar corpo.
        return desembolso.Novo
            ? Results.Created($"/propostas/{id}/contrato", desembolso)
            : Results.Ok(desembolso);
    }

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
