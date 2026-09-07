using System.Net;
using System.Net.Http.Json;
using Credito.Dominio.Amortizacao;
using Credito.Dominio.Propostas;

namespace Credito.Testes.Integracao;

[Collection(ColecaoDaApi.Nome)]
public class SimulacaoTestes
{
    private readonly FabricaDeApi fabrica;

    public SimulacaoTestes(FabricaDeApi fabrica) => this.fabrica = fabrica;

    private sealed record Cadastrada(Guid Id, EstadoDaProposta Estado);

    private sealed record ParcelaSimulada(
        int Numero,
        decimal Amortizacao,
        decimal Juros,
        decimal Valor,
        decimal SaldoDevedor);

    private sealed record Simulacao(
        Guid PropostaId,
        decimal ValorFinanciado,
        decimal TaxaMensal,
        int PrazoEmMeses,
        SistemaDeAmortizacao Sistema,
        int ScoreObservado,
        decimal PrimeiraParcela,
        decimal UltimaParcela,
        decimal TotalPago,
        decimal TotalDeJuros,
        IReadOnlyList<ParcelaSimulada> Parcelas);

    private static async Task<Guid> Cadastrar(
        HttpClient cliente,
        decimal valorSolicitado = 10_000m,
        int prazoEmMeses = 12,
        string sistema = "Price")
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = JsonContent.Create(
                Pedidos.Cadastro(valorSolicitado: valorSolicitado, prazoEmMeses: prazoEmMeses, sistema: sistema)),
        };
        pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());

        var resposta = await cliente.SendAsync(pedido);
        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json))!.Id;
    }

    private static Task<HttpResponseMessage> Simular(HttpClient cliente, Guid id) =>
        cliente.PostAsync($"/propostas/{id}/simulacao", content: null);

    /// <summary>
    /// Score 800 cai na faixa de 700 a 849, cobrada a 1,9% ao mes pela politica semeada.
    /// A simulacao mostra a parcela que o motor de fato ofereceria, e nao a de uma taxa
    /// escolhida por quem pergunta.
    /// </summary>
    [Fact]
    public async Task UsaATaxaDaFaixaDeScoreDaPolitica()
    {
        fabrica.Bureau.Responder(score: 800);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        var resposta = await Simular(cliente, id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var simulacao = await resposta.Content.ReadFromJsonAsync<Simulacao>(Pedidos.Json);

        Assert.Equal(0.019m, simulacao!.TaxaMensal);
        Assert.Equal(800, simulacao.ScoreObservado);
        Assert.Equal(939.80m, simulacao.PrimeiraParcela);
        Assert.Equal(939.78m, simulacao.UltimaParcela);
        Assert.Equal(11_277.58m, simulacao.TotalPago);
        Assert.Equal(10_000m, simulacao.Parcelas.Sum(parcela => parcela.Amortizacao));
    }

    [Fact]
    public async Task ScorePiorProduzTaxaMaiorEParcelaMaisCara()
    {
        var cliente = fabrica.CreateClient();

        fabrica.Bureau.Responder(score: 900);
        var comScoreOtimo = await (await Simular(cliente, await Cadastrar(cliente)))
            .Content.ReadFromJsonAsync<Simulacao>(Pedidos.Json);

        fabrica.Bureau.Responder(score: 550);
        var comScoreFraco = await (await Simular(cliente, await Cadastrar(cliente)))
            .Content.ReadFromJsonAsync<Simulacao>(Pedidos.Json);

        Assert.Equal(0.012m, comScoreOtimo!.TaxaMensal);
        Assert.Equal(0.029m, comScoreFraco!.TaxaMensal);
        Assert.True(comScoreFraco.PrimeiraParcela > comScoreOtimo.PrimeiraParcela);
    }

    [Fact]
    public async Task RespeitaOSistemaEscolhidoNaProposta()
    {
        fabrica.Bureau.Responder(score: 800);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente, valorSolicitado: 12_000m, sistema: "Sac");

        var simulacao = await (await Simular(cliente, id))
            .Content.ReadFromJsonAsync<Simulacao>(Pedidos.Json);

        Assert.Equal(SistemaDeAmortizacao.Sac, simulacao!.Sistema);
        Assert.True(simulacao.UltimaParcela < simulacao.PrimeiraParcela);
        Assert.Equal(12_000m, simulacao.Parcelas.Sum(parcela => parcela.Amortizacao));
    }

    [Fact]
    public async Task SimularNaoMudaOEstadoDaProposta()
    {
        fabrica.Bureau.Responder(score: 800);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        await Simular(cliente, id);

        var detalhe = await cliente.GetFromJsonAsync<Cadastrada>($"/propostas/{id}", Pedidos.Json);

        Assert.Equal(EstadoDaProposta.Rascunho, detalhe!.Estado);
    }

    /// <summary>
    /// Vinte reais em 360 meses passa na validacao do cadastro mas nao se representa em
    /// centavos. Na simulacao isso e erro do pedido — ao contrario da analise, que devolve
    /// laudo explicando.
    /// </summary>
    [Fact]
    public async Task RecusaCombinacaoQueNaoFechaEmCentavos()
    {
        fabrica.Bureau.Responder(score: 800);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente, valorSolicitado: 20m, prazoEmMeses: 360);

        var resposta = await Simular(cliente, id);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains(
            "nao fecham em centavos",
            await resposta.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PropostaInexistenteDevolveNaoEncontrada() =>
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await Simular(fabrica.CreateClient(), Guid.NewGuid())).StatusCode);
}
