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

    private static Task<HttpResponseMessage> Simular(HttpClient cliente, Guid id, decimal taxaMensal) =>
        cliente.PostAsJsonAsync($"/propostas/{id}/simulacao", new { taxaMensal });

    [Fact]
    public async Task DevolveOCronogramaCompletoDaTabelaPrice()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        var resposta = await Simular(cliente, id, 0.01m);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var simulacao = await resposta.Content.ReadFromJsonAsync<Simulacao>(Pedidos.Json);

        Assert.Equal(id, simulacao!.PropostaId);
        Assert.Equal(SistemaDeAmortizacao.Price, simulacao.Sistema);
        Assert.Equal(12, simulacao.Parcelas.Count);
        Assert.Equal(888.49m, simulacao.PrimeiraParcela);
        Assert.Equal(10_000m, simulacao.Parcelas.Sum(parcela => parcela.Amortizacao));
        Assert.Equal(simulacao.TotalPago - 10_000m, simulacao.TotalDeJuros);
    }

    [Fact]
    public async Task RespeitaOSistemaEscolhidoNaProposta()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente, valorSolicitado: 12_000m, sistema: "Sac");

        var simulacao = await (await Simular(cliente, id, 0.01m))
            .Content.ReadFromJsonAsync<Simulacao>(Pedidos.Json);

        Assert.Equal(SistemaDeAmortizacao.Sac, simulacao!.Sistema);
        Assert.Equal(1_120m, simulacao.PrimeiraParcela);
        Assert.True(simulacao.UltimaParcela < simulacao.PrimeiraParcela);
    }

    [Fact]
    public async Task SimularNaoMudaOEstadoDaProposta()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        await Simular(cliente, id, 0.01m);

        var detalhe = await cliente.GetFromJsonAsync<Cadastrada>($"/propostas/{id}", Pedidos.Json);

        Assert.Equal(EstadoDaProposta.Rascunho, detalhe!.Estado);
    }

    [Fact]
    public async Task SemJurosOTotalPagoEhOValorPedido()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente, valorSolicitado: 1_200m);

        var simulacao = await (await Simular(cliente, id, 0m))
            .Content.ReadFromJsonAsync<Simulacao>(Pedidos.Json);

        Assert.Equal(1_200m, simulacao!.TotalPago);
        Assert.Equal(0m, simulacao.TotalDeJuros);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.89)]
    public async Task RecusaTaxaForaDaFaixa(decimal taxaMensal)
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        Assert.Equal(HttpStatusCode.BadRequest, (await Simular(cliente, id, taxaMensal)).StatusCode);
    }

    /// <summary>
    /// Vinte reais em 360 meses passa na validacao do cadastro mas nao se representa em
    /// centavos. A recusa precisa chegar como erro do pedido, e nao como 500.
    /// </summary>
    [Fact]
    public async Task RecusaCombinacaoQueNaoFechaEmCentavos()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente, valorSolicitado: 20m, prazoEmMeses: 360);

        var resposta = await Simular(cliente, id, 0.01m);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains(
            "nao fecham em centavos",
            await resposta.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PropostaInexistenteDevolveNaoEncontrada()
    {
        var resposta = await Simular(fabrica.CreateClient(), Guid.NewGuid(), 0.01m);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
