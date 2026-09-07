using Credito.Aplicacao.Analises;
using Credito.Aplicacao.Propostas;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Credito.Api.Propostas;

internal static class EndpointsDePropostas
{
    public const string PoliticaDeLimite = "submissao-de-proposta";

    private const string CabecalhoDeIdempotencia = "Idempotency-Key";
    private const int TamanhoMaximoDaChave = 64;

    public static void MapearPropostas(this IEndpointRouteBuilder rotas)
    {
        var propostas = rotas.MapGroup("/propostas").WithTags("Propostas");

        propostas.MapPost("/", Cadastrar).RequireRateLimiting(PoliticaDeLimite);
        propostas.MapPost("/{id:guid}/analise", Analisar);
        propostas.MapPost("/{id:guid}/simulacao", Simular);
        propostas.MapGet("/{id:guid}", Detalhar);
    }

    private static async Task<IResult> Cadastrar(
        PedidoDeCadastro pedido,
        [FromHeader(Name = CabecalhoDeIdempotencia)] string? chaveDeIdempotencia,
        IValidator<PedidoDeCadastro> validador,
        CadastrarProposta cadastrar,
        CancellationToken cancelamento)
    {
        if (string.IsNullOrWhiteSpace(chaveDeIdempotencia) || chaveDeIdempotencia.Length > TamanhoMaximoDaChave)
        {
            return Results.Problem(
                title: "Chave de idempotencia ausente ou longa demais",
                detail: $"Envie o cabecalho {CabecalhoDeIdempotencia} com ate {TamanhoMaximoDaChave} caracteres.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var validacao = await validador.ValidateAsync(pedido, cancelamento).ConfigureAwait(false);
        if (!validacao.IsValid)
        {
            return Results.ValidationProblem(validacao.ToDictionary());
        }

        var cadastrada = await cadastrar.Executar(
            new CadastroDeProposta(
                chaveDeIdempotencia,
                pedido.Cpf,
                pedido.NomeSolicitante,
                pedido.DataDeNascimento,
                pedido.RendaMensal,
                pedido.ValorSolicitado,
                pedido.PrazoEmMeses,
                pedido.Sistema),
            cancelamento).ConfigureAwait(false);

        var resposta = new RespostaDeCadastro(cadastrada.Id, cadastrada.Estado);

        // 200 no reenvio e 201 na primeira vez: o cliente distingue sem comparar corpo.
        return cadastrada.JaExistia
            ? Results.Ok(resposta)
            : Results.Created($"/propostas/{cadastrada.Id}", resposta);
    }

    // A decisao sai na hora, entao 200 e nao 202: nao ha nada acontecendo depois.
    private static async Task<IResult> Analisar(
        Guid id,
        AnalisarProposta analisar,
        CancellationToken cancelamento) =>
        Results.Ok(await analisar.Executar(id, cancelamento).ConfigureAwait(false));

    // Nao muda estado nem grava nada, entao responde 200 e dispensa corpo: a taxa sai da
    // politica vigente, e nao de quem pergunta.
    private static async Task<IResult> Simular(
        Guid id,
        SimularProposta simular,
        CancellationToken cancelamento) =>
        Results.Ok(await simular.Executar(id, cancelamento).ConfigureAwait(false));

    private static async Task<IResult> Detalhar(
        Guid id,
        ConsultarProposta consultar,
        CancellationToken cancelamento) =>
        Results.Ok(await consultar.Executar(id, cancelamento).ConfigureAwait(false));
}
