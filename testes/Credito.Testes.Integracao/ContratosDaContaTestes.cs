using System.Net;
using System.Net.Http.Json;

namespace Credito.Testes.Integracao;

/// <summary>
/// A rota que a área de empréstimo do banco abre: os contratos de uma conta.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class ContratosDaContaTestes
{
    private readonly FabricaDeApi fabrica;

    public ContratosDaContaTestes(FabricaDeApi fabrica)
    {
        this.fabrica = fabrica;
        fabrica.Banco.Reiniciar();
    }

    private sealed record Cadastrada(Guid Id);

    private sealed record ProximaParcela(int? Numero, DateOnly? Vencimento, decimal? Valor);

    private sealed record ContratoDaConta(
        Guid ContratoId,
        Guid PropostaId,
        decimal ValorFinanciado,
        int PrazoEmMeses,
        decimal TotalPago,
        decimal SaldoAberto,
        int ParcelasPagas,
        bool EstaQuitado,
        DateTimeOffset? DesembolsadoEm,
        ProximaParcela Proxima);

    private sealed record ParcelaDoContrato(int Numero, decimal Valor);

    private sealed record DetalheDoContrato(IReadOnlyList<ParcelaDoContrato> Parcelas);

    private async Task<Guid> Contratar(HttpClient cliente, Guid conta)
    {
        fabrica.Bureau.Responder(score: 800);

        var pedido = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = JsonContent.Create(Pedidos.Cadastro(valorSolicitado: 10_000m, prazoEmMeses: 6)),
        };
        pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());

        var resposta = await cliente.SendAsync(pedido);
        resposta.EnsureSuccessStatusCode();

        var proposta = (await resposta.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json))!.Id;
        (await cliente.PostAsync($"/propostas/{proposta}/analise", content: null)).EnsureSuccessStatusCode();
        (await cliente.PostAsJsonAsync($"/propostas/{proposta}/contrato", new { contaId = conta }))
            .EnsureSuccessStatusCode();

        return proposta;
    }

    private static Task<IReadOnlyList<ContratoDaConta>?> DaConta(HttpClient cliente, Guid conta) =>
        cliente.GetFromJsonAsync<IReadOnlyList<ContratoDaConta>>($"/contratos?contaId={conta}", Pedidos.Json);

    [Fact]
    public async Task ListaOContratoDaContaComOSaldoEmAberto()
    {
        var cliente = fabrica.CreateClient();
        var conta = Guid.CreateVersion7();
        var proposta = await Contratar(cliente, conta);

        var contratos = (await DaConta(cliente, conta))!;

        var contrato = Assert.Single(contratos);
        Assert.Equal(proposta, contrato.PropostaId);
        Assert.Equal(10_000m, contrato.ValorFinanciado);
        Assert.Equal(0m, contrato.TotalPago);
        Assert.Equal(0, contrato.ParcelasPagas);
        Assert.False(contrato.EstaQuitado);
        Assert.Null(contrato.DesembolsadoEm);
        Assert.Equal(1, contrato.Proxima.Numero);
    }

    /// <summary>
    /// O saldo em aberto é a soma das parcelas que faltam, e o total pago a das que caíram.
    /// Somados, dão o cronograma inteiro — nada pode sumir entre as duas colunas.
    /// </summary>
    [Fact]
    public async Task PagarUmaParcelaMoveOValorDeUmaColunaParaAOutra()
    {
        var cliente = fabrica.CreateClient();
        var conta = Guid.CreateVersion7();
        var proposta = await Contratar(cliente, conta);
        (await cliente.PostAsync($"/propostas/{proposta}/contrato/desembolso", content: null))
            .EnsureSuccessStatusCode();

        var antes = (await DaConta(cliente, conta))![0];

        var pedido = new HttpRequestMessage(
            HttpMethod.Post,
            $"/propostas/{proposta}/contrato/parcelas/1/pagamento");
        pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());
        (await cliente.SendAsync(pedido)).EnsureSuccessStatusCode();

        var depois = (await DaConta(cliente, conta))![0];

        Assert.Equal(1, depois.ParcelasPagas);
        Assert.Equal(2, depois.Proxima.Numero);
        Assert.Equal(antes.SaldoAberto + antes.TotalPago, depois.SaldoAberto + depois.TotalPago);
        Assert.True(depois.TotalPago > antes.TotalPago);
        Assert.NotNull(depois.DesembolsadoEm);
    }

    [Fact]
    public async Task ContratoQuitadoNaoTemProximaParcela()
    {
        var cliente = fabrica.CreateClient();
        var conta = Guid.CreateVersion7();
        var proposta = await Contratar(cliente, conta);
        (await cliente.PostAsync($"/propostas/{proposta}/contrato/desembolso", content: null))
            .EnsureSuccessStatusCode();

        var parcelas = (await cliente.GetFromJsonAsync<DetalheDoContrato>(
            $"/propostas/{proposta}/contrato",
            Pedidos.Json))!.Parcelas;

        fabrica.Banco.Depositar(conta, parcelas.Sum(parcela => parcela.Valor));

        foreach (var parcela in parcelas)
        {
            var pedido = new HttpRequestMessage(
                HttpMethod.Post,
                $"/propostas/{proposta}/contrato/parcelas/{parcela.Numero}/pagamento");
            pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());
            (await cliente.SendAsync(pedido)).EnsureSuccessStatusCode();
        }

        var contrato = (await DaConta(cliente, conta))![0];

        Assert.True(contrato.EstaQuitado);
        Assert.Equal(0m, contrato.SaldoAberto);
        Assert.Null(contrato.Proxima.Numero);
        Assert.Null(contrato.Proxima.Vencimento);
    }

    [Fact]
    public async Task DoisContratosDaMesmaContaVemOsDois()
    {
        var cliente = fabrica.CreateClient();
        var conta = Guid.CreateVersion7();
        await Contratar(cliente, conta);
        await Contratar(cliente, conta);

        Assert.Equal(2, (await DaConta(cliente, conta))!.Count);
    }

    /// <summary>
    /// Cada conta vê só o que é seu. Sem o filtro, a área de empréstimo mostraria o
    /// contrato de outra pessoa.
    /// </summary>
    [Fact]
    public async Task ContratoDeOutraContaNaoAparece()
    {
        var cliente = fabrica.CreateClient();
        var minha = Guid.CreateVersion7();
        var outra = Guid.CreateVersion7();
        await Contratar(cliente, outra);

        Assert.Empty((await DaConta(cliente, minha))!);
    }

    // Contrato sem conta não pertence a conta nenhuma, e não pode cair na lista de todas.
    [Fact]
    public async Task ContratoSemContaNaoAparecePorContaNenhuma()
    {
        var cliente = fabrica.CreateClient();
        fabrica.Bureau.Responder(score: 800);

        var pedido = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = JsonContent.Create(Pedidos.Cadastro(valorSolicitado: 10_000m, prazoEmMeses: 6)),
        };
        pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());
        var proposta = (await (await cliente.SendAsync(pedido))
            .Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json))!.Id;

        (await cliente.PostAsync($"/propostas/{proposta}/analise", content: null)).EnsureSuccessStatusCode();
        (await cliente.PostAsJsonAsync($"/propostas/{proposta}/contrato", new { })).EnsureSuccessStatusCode();

        Assert.Empty((await DaConta(cliente, Guid.CreateVersion7()))!);
    }

    [Fact]
    public async Task ContaSemEmprestimoDevolveListaVazia() =>
        Assert.Empty((await DaConta(fabrica.CreateClient(), Guid.CreateVersion7()))!);

    [Fact]
    public async Task SemContaIdEhRecusado() =>
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await fabrica.CreateClient().GetAsync("/contratos")).StatusCode);
}
