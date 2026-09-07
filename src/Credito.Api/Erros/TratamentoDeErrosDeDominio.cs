using Credito.Aplicacao.Erros;
using Credito.Dominio.Erros;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Credito.Api.Erros;

/// <summary>
/// Traduz violacao de regra em resposta HTTP. So trata o que e erro do pedido — o
/// resto sobe e vira 500, porque falha nossa nao pode virar 400 e sumir do radar.
/// </summary>
public sealed partial class TratamentoDeErrosDeDominio : IExceptionHandler
{
    private readonly IProblemDetailsService detalhes;
    private readonly ILogger<TratamentoDeErrosDeDominio> registro;

    public TratamentoDeErrosDeDominio(IProblemDetailsService detalhes, ILogger<TratamentoDeErrosDeDominio> registro)
    {
        this.detalhes = detalhes;
        this.registro = registro;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (Traduzir(exception) is not { } traducao)
        {
            return false;
        }

        var (status, titulo) = traducao;

        httpContext.Response.StatusCode = status;

        // Sem isto o erro de dominio nao deixa rastro nenhum: o registro de requisicao
        // roda por fora daqui e so enxerga o codigo de status, sem dizer o que houve.
        RegistrarRecusa(status, titulo, exception.Message);

        // As mensagens de dominio nao carregam CPF nem renda — ha teste garantindo isso
        // para o CPF. Por isso da para devolve-las ao cliente sem reescrever.
        return await detalhes.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = exception.Message,
            },
        }).ConfigureAwait(false);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Pedido recusado com {Status} ({Titulo}): {Motivo}")]
    private partial void RegistrarRecusa(int status, string titulo, string motivo);

    private static (int Status, string Titulo)? Traduzir(Exception excecao) => excecao switch
    {
        PropostaNaoEncontradaException => (StatusCodes.Status404NotFound, "Proposta nao encontrada"),
        ContratoNaoEncontradoException => (StatusCodes.Status404NotFound, "Contrato nao encontrado"),
        ChaveDeIdempotenciaReutilizadaException =>
            (StatusCodes.Status409Conflict, "Chave de idempotencia reutilizada"),
        TransicaoInvalidaException => (StatusCodes.Status409Conflict, "Transicao de estado invalida"),

        // A recusa do banco e resposta, nao falha: saldo insuficiente e conta bloqueada
        // sao decisoes do outro lado, e repetir o pedido nao muda nada.
        ContaBancariaRecusouException => (StatusCodes.Status409Conflict, "A conta bancaria recusou"),

        // 502 e nao 500: o problema esta no servico de tras, e quem chama pode repetir com
        // a mesma chave sem risco de desembolsar duas vezes.
        ContaBancariaIndisponivelException =>
            (StatusCodes.Status502BadGateway, "Plataforma bancaria indisponivel"),

        // Outra requisicao mexeu na mesma proposta entre a leitura e a gravacao.
        DbUpdateConcurrencyException =>
            (StatusCodes.Status409Conflict, "A proposta mudou durante o pedido — tente de novo"),

        DominioException => (StatusCodes.Status400BadRequest, "Pedido invalido"),
        _ => null,
    };
}
