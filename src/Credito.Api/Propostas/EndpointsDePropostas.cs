using Credito.Aplicacao.Analises;
using Credito.Aplicacao.Consultas;
using Credito.Dominio.Propostas;
using Credito.Aplicacao.Propostas;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Credito.Api.Propostas;

internal static class EndpointsDePropostas
{
    public const string PoliticaDeSubmissao = "submissao-de-proposta";
    public const string PoliticaDeBusca = "busca-por-cpf";

    private const string CabecalhoDeIdempotencia = "Idempotency-Key";
    private const int TamanhoMaximoDaChave = 64;

    public static void MapearPropostas(this IEndpointRouteBuilder rotas)
    {
        var propostas = rotas.MapGroup("/propostas").WithTags("Propostas");

        propostas.MapPost("/", Cadastrar).RequireRateLimiting(PoliticaDeSubmissao);
        propostas.MapPost("/{id:guid}/analise", Analisar);
        propostas.MapPost("/{id:guid}/simulacao", Simular);
        propostas.MapPost("/{id:guid}/cancelamento", Cancelar);
        propostas.MapGet("/{id:guid}", Detalhar);
        propostas.MapGet("/", Listar);
        propostas.MapPost("/busca", Buscar).RequireRateLimiting(PoliticaDeBusca);
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

    private static async Task<IResult> Cancelar(
        Guid id,
        CancelarProposta cancelar,
        CancellationToken cancelamento) =>
        Results.Ok(new RespostaDeCadastro(id, await cancelar.Executar(id, cancelamento).ConfigureAwait(false)));

    /// <remarks>
    /// Sem CPF entre os filtros: aqui ele viraria cadeia de consulta e apareceria em
    /// registro de acesso e historico. Busca por CPF tem rota propria, com o numero
    /// no corpo.
    /// </remarks>
    private static async Task<IResult> Listar(
        ListarPropostas listar,
        CancellationToken cancelamento,
        EstadoDaProposta? estado = null,
        DateTimeOffset? de = null,
        DateTimeOffset? ate = null,
        string? cursor = null,
        int? tamanho = null) =>
        Results.Ok(await listar
            .Executar(new PedidoDeListagem(estado, de, ate, Cpf: null, cursor, tamanho), cancelamento)
            .ConfigureAwait(false));

    /// <remarks>
    /// POST porque o CPF vai no corpo. Nao e uma escrita disfarcada: e o unico jeito de
    /// procurar por CPF sem deixa-lo em registro de acesso, historico de navegador e
    /// cache de intermediario, que e onde a URL de um GET termina.
    /// </remarks>
    private static async Task<IResult> Buscar(
        PedidoDeBusca pedido,
        ListarPropostas listar,
        CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        // Sem CPF isto viraria a listagem inteira por uma rota que nao tem esse contrato
        // — e que gasta o limite da busca em vez do limite da listagem.
        if (string.IsNullOrWhiteSpace(pedido.Cpf))
        {
            return Results.Problem(
                title: "CPF obrigatorio",
                detail: "Envie o CPF no corpo do pedido de busca.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(await listar
            .Executar(
                new PedidoDeListagem(
                    pedido.Estado,
                    pedido.De,
                    pedido.Ate,
                    pedido.Cpf,
                    pedido.Cursor,
                    pedido.Tamanho),
                cancelamento)
            .ConfigureAwait(false));
    }

    private static async Task<IResult> Detalhar(
        Guid id,
        ConsultarProposta consultar,
        CancellationToken cancelamento) =>
        Results.Ok(await consultar.Executar(id, cancelamento).ConfigureAwait(false));
}
