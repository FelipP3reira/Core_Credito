using System.Net;
using System.Net.Http.Json;
using Credito.Dominio.Amortizacao;
using Credito.Dominio.Propostas;

namespace Credito.Testes.Integracao;

[Collection(ColecaoDaApi.Nome)]
public class ContratacaoTestes
{
    private readonly FabricaDeApi fabrica;

    public ContratacaoTestes(FabricaDeApi fabrica) => this.fabrica = fabrica;

    private sealed record Cadastrada(Guid Id, EstadoDaProposta Estado);

    private sealed record Decisao(bool Aprovada, decimal TaxaMensalAplicada);

    private sealed record ParcelaDoContrato(
        int Numero,
        DateOnly Vencimento,
        decimal Amortizacao,
        decimal Juros,
        decimal Valor,
        decimal SaldoDevedor,
        DateTimeOffset? PagaEm);

    private sealed record Contrato(
        Guid Id,
        Guid PropostaId,
        decimal ValorFinanciado,
        decimal TaxaMensal,
        SistemaDeAmortizacao Sistema,
        int PrazoEmMeses,
        DateOnly PrimeiroVencimento,
        DateTimeOffset AssinadoEm,
        decimal TotalPago,
        decimal SaldoAberto,
        bool EstaQuitado,
        IReadOnlyList<ParcelaDoContrato> Parcelas);

    private sealed record Pagamento(
        Guid PropostaId,
        int Numero,
        DateTimeOffset PagaEm,
        decimal Valor,
        decimal TotalPago,
        decimal SaldoAberto,
        bool ContratoQuitado,
        EstadoDaProposta EstadoDaProposta);

    private sealed record Detalhe(Guid Id, EstadoDaProposta Estado);

    private static async Task<Guid> Cadastrar(
        HttpClient cliente,
        decimal valorSolicitado = 10_000m,
        int prazoEmMeses = 6)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = JsonContent.Create(Pedidos.Cadastro(
                valorSolicitado: valorSolicitado,
                prazoEmMeses: prazoEmMeses)),
        };
        pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());

        var resposta = await cliente.SendAsync(pedido);
        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json))!.Id;
    }

    private async Task<Guid> Aprovada(HttpClient cliente, decimal valor = 10_000m, int prazo = 6)
    {
        fabrica.Bureau.Responder(score: 800);

        var id = await Cadastrar(cliente, valor, prazo);
        (await cliente.PostAsync($"/propostas/{id}/analise", content: null)).EnsureSuccessStatusCode();

        return id;
    }

    private static Task<HttpResponseMessage> Contratar(HttpClient cliente, Guid id, DateOnly? primeiroVencimento = null) =>
        cliente.PostAsJsonAsync($"/propostas/{id}/contrato", new { primeiroVencimento });

    private static HttpRequestMessage Cobranca(Guid id, int numero, string chave)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, $"/propostas/{id}/contrato/parcelas/{numero}/pagamento");
        pedido.Headers.Add("Idempotency-Key", chave);
        return pedido;
    }

    [Fact]
    public async Task ContrataPropostaAprovadaECongelaOCronograma()
    {
        var cliente = fabrica.CreateClient();
        var id = await Aprovada(cliente);

        var resposta = await Contratar(cliente, id);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var contrato = await resposta.Content.ReadFromJsonAsync<Contrato>(Pedidos.Json);

        Assert.Equal(id, contrato!.PropostaId);
        Assert.Equal(6, contrato.Parcelas.Count);
        Assert.Equal(10_000m, contrato.ValorFinanciado);
        Assert.Equal(10_000m, contrato.Parcelas.Sum(parcela => parcela.Amortizacao));
        Assert.False(contrato.EstaQuitado);
        Assert.Equal(0m, contrato.TotalPago);
        Assert.Equal(contrato.Parcelas.Sum(parcela => parcela.Valor), contrato.SaldoAberto);
        Assert.All(contrato.Parcelas, parcela => Assert.Null(parcela.PagaEm));
    }

    /// <summary>
    /// A proposta foi aprovada sob uma taxa e e essa que se contrata. Politica que mude
    /// depois vale para a proxima analise, nao para uma aprovacao ja dada.
    /// </summary>
    [Fact]
    public async Task UsaATaxaCongeladaNaDecisao()
    {
        var cliente = fabrica.CreateClient();

        fabrica.Bureau.Responder(score: 550);
        var id = await Cadastrar(cliente);
        var decisao = await (await cliente.PostAsync($"/propostas/{id}/analise", content: null))
            .Content.ReadFromJsonAsync<Decisao>(Pedidos.Json);

        // Score muda entre a analise e a contratacao: a taxa do contrato nao pode mudar junto.
        fabrica.Bureau.Responder(score: 950);
        var contrato = await (await Contratar(cliente, id)).Content.ReadFromJsonAsync<Contrato>(Pedidos.Json);

        Assert.True(decisao!.Aprovada);
        Assert.Equal(0.029m, decisao.TaxaMensalAplicada);
        Assert.Equal(decisao.TaxaMensalAplicada, contrato!.TaxaMensal);
    }

    [Fact]
    public async Task PrimeiroVencimentoPadraoEhTrintaDiasDepois()
    {
        var cliente = fabrica.CreateClient();
        var id = await Aprovada(cliente);

        var contrato = await (await Contratar(cliente, id)).Content.ReadFromJsonAsync<Contrato>(Pedidos.Json);

        var esperado = DateOnly.FromDateTime(fabrica.Relogio.GetUtcNow().UtcDateTime).AddDays(30);

        Assert.Equal(esperado, contrato!.PrimeiroVencimento);
        Assert.Equal(esperado, contrato.Parcelas[0].Vencimento);
        Assert.Equal(esperado.AddMonths(5), contrato.Parcelas[^1].Vencimento);
    }

    [Fact]
    public async Task AceitaPrimeiroVencimentoEscolhido()
    {
        var cliente = fabrica.CreateClient();
        var id = await Aprovada(cliente);
        var escolhido = DateOnly.FromDateTime(fabrica.Relogio.GetUtcNow().UtcDateTime).AddDays(15);

        var contrato = await (await Contratar(cliente, id, escolhido))
            .Content.ReadFromJsonAsync<Contrato>(Pedidos.Json);

        Assert.Equal(escolhido, contrato!.PrimeiroVencimento);
    }

    [Fact]
    public async Task RecusaPrimeiroVencimentoForaDaJanela()
    {
        var cliente = fabrica.CreateClient();
        var hoje = DateOnly.FromDateTime(fabrica.Relogio.GetUtcNow().UtcDateTime);

        var noPassado = await Contratar(cliente, await Aprovada(cliente), hoje.AddDays(-1));
        var longeDemais = await Contratar(cliente, await Aprovada(cliente), hoje.AddDays(90));

        Assert.Equal(HttpStatusCode.BadRequest, noPassado.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longeDemais.StatusCode);
    }

    [Fact]
    public async Task PropostaSemAprovacaoNaoContrata()
    {
        var cliente = fabrica.CreateClient();
        var emRascunho = await Cadastrar(cliente);

        var resposta = await Contratar(cliente, emRascunho);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains(
            "nao tem aprovacao",
            await resposta.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PropostaNegadaNaoContrata()
    {
        var cliente = fabrica.CreateClient();
        fabrica.Bureau.Responder(score: 200);
        var id = await Cadastrar(cliente);
        await cliente.PostAsync($"/propostas/{id}/analise", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, (await Contratar(cliente, id)).StatusCode);
    }

    [Fact]
    public async Task ContratarDuasVezesEhRecusado()
    {
        var cliente = fabrica.CreateClient();
        var id = await Aprovada(cliente);

        await Contratar(cliente, id);
        var segunda = await Contratar(cliente, id);

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task ConsultaOContratoDepoisDeAssinado()
    {
        var cliente = fabrica.CreateClient();
        var id = await Aprovada(cliente);
        await Contratar(cliente, id);

        var contrato = await cliente.GetFromJsonAsync<Contrato>($"/propostas/{id}/contrato", Pedidos.Json);

        Assert.Equal(6, contrato!.Parcelas.Count);
        Assert.Equal(Enumerable.Range(1, 6), contrato.Parcelas.Select(parcela => parcela.Numero));
    }

    [Fact]
    public async Task PropostaSemContratoDevolveNaoEncontrado()
    {
        var cliente = fabrica.CreateClient();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await cliente.GetAsync($"/propostas/{await Cadastrar(cliente)}/contrato")).StatusCode);
    }

    /// <summary>
    /// Aprovacao vencida expira a proposta na hora em que alguem tenta usa-la, e nao por
    /// varredura periodica. E o unico momento em que a diferenca importa.
    /// </summary>
    [Fact]
    public async Task AprovacaoVencidaExpiraAPropostaEmVezDeSoRecusar()
    {
        var cliente = fabrica.CreateClient();
        var id = await Aprovada(cliente);

        try
        {
            fabrica.Relogio.Avancar(TimeSpan.FromDays(31));

            var resposta = await Contratar(cliente, id);

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            Assert.Contains("venceu", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            var detalhe = await cliente.GetFromJsonAsync<Detalhe>($"/propostas/{id}", Pedidos.Json);
            Assert.Equal(EstadoDaProposta.Expirada, detalhe!.Estado);
        }
        finally
        {
            fabrica.Relogio.Reiniciar();
        }
    }

    [Fact]
    public async Task PagarUmaParcelaMoveOSaldoSemQuitar()
    {
        var cliente = fabrica.CreateClient();
        var id = await Aprovada(cliente);
        var contrato = await (await Contratar(cliente, id)).Content.ReadFromJsonAsync<Contrato>(Pedidos.Json);

        var resposta = await cliente.SendAsync(Cobranca(id, 1, Pedidos.ChaveNova()));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var pagamento = await resposta.Content.ReadFromJsonAsync<Pagamento>(Pedidos.Json);

        Assert.Equal(1, pagamento!.Numero);
        Assert.Equal(contrato!.Parcelas[0].Valor, pagamento.Valor);
        Assert.Equal(contrato.SaldoAberto - pagamento.Valor, pagamento.SaldoAberto);
        Assert.False(pagamento.ContratoQuitado);
        Assert.Equal(EstadoDaProposta.Contratada, pagamento.EstadoDaProposta);
    }

    /// <summary>A liquidacao nao e um pedido a parte: acontece quando a ultima parcela cai.</summary>
    [Fact]
    public async Task PagarTodasAsParcelasLiquidaAProposta()
    {
        var cliente = fabrica.CreateClient();
        var id = await Aprovada(cliente);
        await Contratar(cliente, id);

        Pagamento? ultimo = null;
        foreach (var numero in Enumerable.Range(1, 6))
        {
            var resposta = await cliente.SendAsync(Cobranca(id, numero, Pedidos.ChaveNova()));
            ultimo = await resposta.Content.ReadFromJsonAsync<Pagamento>(Pedidos.Json);
        }

        Assert.True(ultimo!.ContratoQuitado);
        Assert.Equal(0m, ultimo.SaldoAberto);
        Assert.Equal(EstadoDaProposta.Liquidada, ultimo.EstadoDaProposta);

        var detalhe = await cliente.GetFromJsonAsync<Detalhe>($"/propostas/{id}", Pedidos.Json);
        Assert.Equal(EstadoDaProposta.Liquidada, detalhe!.Estado);
    }

    [Fact]
    public async Task ReenvioDaMesmaCobrancaNaoMudaNada()
    {
        var cliente = fabrica.CreateClient();
        var id = await Aprovada(cliente);
        await Contratar(cliente, id);
        var chave = Pedidos.ChaveNova();

        var primeira = await (await cliente.SendAsync(Cobranca(id, 2, chave)))
            .Content.ReadFromJsonAsync<Pagamento>(Pedidos.Json);
        var segunda = await (await cliente.SendAsync(Cobranca(id, 2, chave)))
            .Content.ReadFromJsonAsync<Pagamento>(Pedidos.Json);

        Assert.Equal(primeira!.PagaEm, segunda!.PagaEm);
        Assert.Equal(primeira.TotalPago, segunda.TotalPago);
    }

    [Fact]
    public async Task OutraCobrancaSobreParcelaJaPagaEhRecusada()
    {
        var cliente = fabrica.CreateClient();
        var id = await Aprovada(cliente);
        await Contratar(cliente, id);

        await cliente.SendAsync(Cobranca(id, 2, Pedidos.ChaveNova()));
        var repetida = await cliente.SendAsync(Cobranca(id, 2, Pedidos.ChaveNova()));

        Assert.Equal(HttpStatusCode.BadRequest, repetida.StatusCode);
        Assert.Contains("ja foi paga", await repetida.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecusaPagamentoSemCabecalhoDeIdempotencia()
    {
        var cliente = fabrica.CreateClient();
        var id = await Aprovada(cliente);
        await Contratar(cliente, id);

        var resposta = await cliente.PostAsync($"/propostas/{id}/contrato/parcelas/1/pagamento", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task RecusaParcelaQueNaoExisteNoContrato()
    {
        var cliente = fabrica.CreateClient();
        var id = await Aprovada(cliente);
        await Contratar(cliente, id);

        var resposta = await cliente.SendAsync(Cobranca(id, 99, Pedidos.ChaveNova()));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task PagarContratoInexistenteDevolveNaoEncontrado()
    {
        var cliente = fabrica.CreateClient();

        var resposta = await cliente.SendAsync(Cobranca(await Cadastrar(cliente), 1, Pedidos.ChaveNova()));

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
