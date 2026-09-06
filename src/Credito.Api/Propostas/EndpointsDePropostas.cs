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
        propostas.MapPost("/{id:guid}/analise", EnviarParaAnalise);
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

    private static async Task<IResult> EnviarParaAnalise(
        Guid id,
        EnviarPropostaParaAnalise enviar,
        CancellationToken cancelamento)
    {
        var estado = await enviar.Executar(id, cancelamento).ConfigureAwait(false);

        return Results.Accepted($"/propostas/{id}", new RespostaDeAnalise(id, estado));
    }

    private static async Task<IResult> Detalhar(
        Guid id,
        ConsultarProposta consultar,
        CancellationToken cancelamento) =>
        Results.Ok(await consultar.Executar(id, cancelamento).ConfigureAwait(false));
}
