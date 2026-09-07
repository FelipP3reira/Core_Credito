using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Credito.Dominio.Propostas;

namespace Credito.Testes.Integracao;

[Collection(ColecaoDaApi.Nome)]
public class ResumoTestes
{
    private readonly FabricaDeApi fabrica;

    public ResumoTestes(FabricaDeApi fabrica) => this.fabrica = fabrica;

    private sealed record Cadastrada(Guid Id, EstadoDaProposta Estado);

    private sealed record Contagem(EstadoDaProposta Estado, int Quantidade, decimal ValorSolicitado);

    private sealed record Resumo(
        DateTimeOffset? De,
        DateTimeOffset? Ate,
        int Total,
        decimal ValorSolicitado,
        IReadOnlyList<Contagem> PorEstado);

    private static async Task<Guid> Cadastrar(HttpClient cliente, decimal valorSolicitado)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = JsonContent.Create(Pedidos.Cadastro(valorSolicitado)),
        };
        pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());

        var resposta = await cliente.SendAsync(pedido);
        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json))!.Id;
    }

    private string Agora() =>
        Uri.EscapeDataString(fabrica.Relogio.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));

    private static Task<Resumo?> Resumir(HttpClient cliente, string consulta) =>
        cliente.GetFromJsonAsync<Resumo>($"/propostas/resumo?{consulta}", Pedidos.Json);

    [Fact]
    public async Task ContaESomaPorEstadoDentroDoPeriodo()
    {
        var cliente = fabrica.CreateClient();
        var recorte = Agora();

        await Cadastrar(cliente, 10_000m);
        await Cadastrar(cliente, 25_000m);
        var cancelada = await Cadastrar(cliente, 5_000m);
        await cliente.PostAsync($"/propostas/{cancelada}/cancelamento", content: null);

        var resumo = await Resumir(cliente, $"de={recorte}");

        Assert.Equal(3, resumo!.Total);
        Assert.Equal(40_000m, resumo.ValorSolicitado);

        var rascunho = resumo.PorEstado.Single(linha => linha.Estado == EstadoDaProposta.Rascunho);
        Assert.Equal(2, rascunho.Quantidade);
        Assert.Equal(35_000m, rascunho.ValorSolicitado);

        var cancelou = resumo.PorEstado.Single(linha => linha.Estado == EstadoDaProposta.Cancelada);
        Assert.Equal(1, cancelou.Quantidade);
        Assert.Equal(5_000m, cancelou.ValorSolicitado);
    }

    // Estado sem nenhuma proposta tem que aparecer zerado: linha ausente e ambigua.
    [Fact]
    public async Task TrazTodosOsEstadosMesmoOsZerados()
    {
        var resumo = await Resumir(fabrica.CreateClient(), $"de={Agora()}");

        Assert.Equal(
            Enum.GetValues<EstadoDaProposta>(),
            resumo!.PorEstado.Select(linha => linha.Estado));

        Assert.All(resumo.PorEstado, linha => Assert.Equal(0, linha.Quantidade));
        Assert.Equal(0, resumo.Total);
        Assert.Equal(0m, resumo.ValorSolicitado);
    }

    [Fact]
    public async Task OTotalBateComASomaDasLinhas()
    {
        var cliente = fabrica.CreateClient();
        var recorte = Agora();
        await Cadastrar(cliente, 7_000m);
        await Cadastrar(cliente, 3_000m);

        var resumo = await Resumir(cliente, $"de={recorte}");

        Assert.Equal(resumo!.PorEstado.Sum(linha => linha.Quantidade), resumo.Total);
        Assert.Equal(resumo.PorEstado.Sum(linha => linha.ValorSolicitado), resumo.ValorSolicitado);
    }

    [Fact]
    public async Task PeriodoInvertidoEhPedidoInvalido()
    {
        var resposta = await fabrica.CreateClient().GetAsync(
            $"/propostas/resumo?de={Agora()}&ate={Uri.EscapeDataString("2020-01-01T00:00:00.0000000+00:00")}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task ResumoNaoDevolveDadoDeNinguem()
    {
        var cliente = fabrica.CreateClient();
        var recorte = Agora();
        await Cadastrar(cliente, 9_000m);

        var corpo = await cliente.GetStringAsync($"/propostas/resumo?de={recorte}");

        Assert.DoesNotContain(Pedidos.CpfEmDigitos, corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("Ana Ribeiro", corpo, StringComparison.Ordinal);
    }
}
