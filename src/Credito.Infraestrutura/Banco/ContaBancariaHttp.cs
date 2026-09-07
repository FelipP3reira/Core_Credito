using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;

namespace Credito.Infraestrutura.Banco;

/// <summary>
/// A conta bancaria pela borda HTTP da Plataforma Bancaria.
/// </summary>
/// <remarks>
/// So conhece duas rotas — deposito e saque —, e nao o banco de dados do outro lado. E o
/// que deixa os dois servicos evoluirem separados: o credito depende do contrato publicado,
/// e nao do formato da tabela de lancamentos.
/// </remarks>
public sealed class ContaBancariaHttp : IContaBancaria
{
    private const string CabecalhoDeIdempotencia = "Idempotency-Key";
    private const string CabecalhoDeOperador = "X-Operador";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient cliente;

    public ContaBancariaHttp(HttpClient cliente) => this.cliente = cliente;

    public Task<LancamentoNaConta> Creditar(
        PedidoDeLancamentoNaConta pedido,
        CancellationToken cancelamento) =>
        Movimentar(pedido, "depositos", cancelamento);

    public Task<LancamentoNaConta> Debitar(
        PedidoDeLancamentoNaConta pedido,
        CancellationToken cancelamento) =>
        Movimentar(pedido, "saques", cancelamento);

    private async Task<LancamentoNaConta> Movimentar(
        PedidoDeLancamentoNaConta pedido,
        string rota,
        CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var requisicao = new HttpRequestMessage(
            HttpMethod.Post,
            string.Create(CultureInfo.InvariantCulture, $"/contas/{pedido.ContaId}/{rota}"))
        {
            Content = JsonContent.Create(new { valor = pedido.Valor, descricao = pedido.Descricao }),
        };

        requisicao.Headers.Add(CabecalhoDeIdempotencia, pedido.ChaveIdempotencia);
        requisicao.Headers.Add(CabecalhoDeOperador, pedido.Operador);

        // Rede fora do ar e resposta que nao chega sao o mesmo caso aqui: nao da para saber
        // se o lancamento aconteceu. A chave derivada e o que torna a nova tentativa segura.
        HttpResponseMessage resposta;
        try
        {
            resposta = await cliente.SendAsync(requisicao, cancelamento).ConfigureAwait(false);
        }
        catch (Exception erro) when (erro is HttpRequestException or TaskCanceledException)
        {
            throw new ContaBancariaIndisponivelException(
                "A plataforma bancaria nao respondeu. O pedido pode ser repetido com a mesma chave.",
                erro);
        }

        using (resposta)
        {
            if (resposta.IsSuccessStatusCode)
            {
                return await Ler(resposta, cancelamento).ConfigureAwait(false);
            }

            throw await Traduzir(resposta, cancelamento).ConfigureAwait(false);
        }
    }

    /// <remarks>
    /// 201 na primeira vez e 200 no reenvio reconhecido pela chave. E dai que sai o
    /// <c>Novo</c>: sem ele, quem chama nao consegue distinguir "o dinheiro acabou de
    /// entrar" de "o dinheiro ja tinha entrado antes".
    /// </remarks>
    private static async Task<LancamentoNaConta> Ler(
        HttpResponseMessage resposta,
        CancellationToken cancelamento)
    {
        var corpo = await resposta.Content
            .ReadFromJsonAsync<RespostaDeLancamento>(Json, cancelamento)
            .ConfigureAwait(false)
            ?? throw new ContaBancariaIndisponivelException(
                "A plataforma bancaria devolveu sucesso com corpo vazio.");

        return new LancamentoNaConta(
            corpo.Id,
            corpo.Sequencia,
            corpo.SaldoDepois,
            corpo.CriadoEm,
            resposta.StatusCode == HttpStatusCode.Created);
    }

    /// <summary>
    /// Separa o "nao" do "nao deu".
    /// </summary>
    /// <remarks>
    /// 4xx e decisao do banco e nao adianta repetir; 5xx e falha e repetir e o certo. O
    /// titulo do ProblemDetails do outro lado vai junto porque "saldo insuficiente" e
    /// "conta bloqueada" pedem acoes diferentes de quem esta na ponta.
    /// </remarks>
    private static async Task<Exception> Traduzir(
        HttpResponseMessage resposta,
        CancellationToken cancelamento)
    {
        var motivo = await Motivo(resposta, cancelamento).ConfigureAwait(false);

        return (int)resposta.StatusCode is >= 400 and < 500
            ? new ContaBancariaRecusouException($"A plataforma bancaria recusou: {motivo}")
            : new ContaBancariaIndisponivelException(
                $"A plataforma bancaria falhou ({(int)resposta.StatusCode}): {motivo}");
    }

    private static async Task<string> Motivo(HttpResponseMessage resposta, CancellationToken cancelamento)
    {
        try
        {
            var problema = await resposta.Content
                .ReadFromJsonAsync<ProblemaDoBanco>(Json, cancelamento)
                .ConfigureAwait(false);

            return string.Join(
                " ",
                new[] { problema?.Title, problema?.Detail }.Where(parte => !string.IsNullOrWhiteSpace(parte)));
        }
        catch (Exception erro) when (erro is JsonException or NotSupportedException)
        {
            // Corpo que nao e ProblemDetails ainda diz alguma coisa pelo codigo de status;
            // o que nao pode e a falha de leitura virar a falha que o chamador ve.
            return resposta.ReasonPhrase ?? "sem detalhe";
        }
    }

    private sealed record RespostaDeLancamento(
        Guid Id,
        long Sequencia,
        decimal SaldoDepois,
        DateTimeOffset CriadoEm);

    private sealed record ProblemaDoBanco(string? Title, string? Detail);
}
