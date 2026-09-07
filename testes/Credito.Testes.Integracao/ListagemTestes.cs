using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Credito.Dominio.Amortizacao;
using Credito.Dominio.Propostas;

namespace Credito.Testes.Integracao;

[Collection(ColecaoDaApi.Nome)]
public class ListagemTestes
{
    private readonly FabricaDeApi fabrica;

    public ListagemTestes(FabricaDeApi fabrica) => this.fabrica = fabrica;

    private sealed record Cadastrada(Guid Id, EstadoDaProposta Estado);

    private sealed record Linha(
        Guid Id,
        EstadoDaProposta Estado,
        string NomeSolicitante,
        decimal ValorSolicitado,
        int PrazoEmMeses,
        SistemaDeAmortizacao Sistema,
        DateTimeOffset CriadaEm,
        DateTimeOffset AtualizadaEm);

    private sealed record Pagina(IReadOnlyList<Linha> Itens, string? Proximo);

    private static async Task<Guid> Cadastrar(HttpClient cliente, decimal valorSolicitado = 20_000m)
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

    /// <summary>
    /// Corta pelo instante em que o teste comecou. A colecao compartilha um banco so, e
    /// sem o recorte cada teste veria tambem as propostas de todos os que rodaram antes.
    /// </summary>
    private string Agora() =>
        Uri.EscapeDataString(fabrica.Relogio.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));

    private static Task<Pagina?> Listar(HttpClient cliente, string consulta) =>
        cliente.GetFromJsonAsync<Pagina>($"/propostas?{consulta}", Pedidos.Json);

    [Fact]
    public async Task ListaAsMaisNovasPrimeiro()
    {
        var cliente = fabrica.CreateClient();
        var recorte = Agora();

        var primeira = await Cadastrar(cliente);
        var segunda = await Cadastrar(cliente);
        var terceira = await Cadastrar(cliente);

        var pagina = await Listar(cliente, $"de={recorte}&tamanho=50");

        Assert.Equal([terceira, segunda, primeira], pagina!.Itens.Select(linha => linha.Id));
        Assert.Null(pagina.Proximo);
    }

    [Fact]
    public async Task PaginaPorCursorSemRepetirNemPularLinha()
    {
        var cliente = fabrica.CreateClient();
        var recorte = Agora();

        var criadas = new List<Guid>();
        for (var contador = 0; contador < 5; contador++)
        {
            criadas.Add(await Cadastrar(cliente));
        }

        var vistas = new List<Guid>();
        var paginas = 0;
        string? cursor = null;

        do
        {
            var consulta = $"de={recorte}&tamanho=2";
            if (cursor is not null)
            {
                consulta += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            var pagina = await Listar(cliente, consulta);

            Assert.True(pagina!.Itens.Count <= 2, "a pagina veio maior do que o tamanho pedido");
            vistas.AddRange(pagina.Itens.Select(linha => linha.Id));
            cursor = pagina.Proximo;
            paginas++;
        }
        while (cursor is not null && paginas < 10);

        // Cinco linhas de duas em duas: duas cheias e uma com a sobra.
        Assert.Equal(3, paginas);
        criadas.Reverse();
        Assert.Equal(criadas, vistas);
    }

    [Fact]
    public async Task FiltraPorEstado()
    {
        var cliente = fabrica.CreateClient();
        var recorte = Agora();

        var cancelada = await Cadastrar(cliente);
        await Cadastrar(cliente);
        await cliente.PostAsync($"/propostas/{cancelada}/cancelamento", content: null);

        var pagina = await Listar(cliente, $"de={recorte}&estado=Cancelada&tamanho=50");

        var linha = Assert.Single(pagina!.Itens);
        Assert.Equal(cancelada, linha.Id);
    }

    // A mascara so existe depois de decifrar: listagem que mostrasse CPF decifraria uma
    // pagina inteira de numeros para exibir tres digitos de cada.
    [Fact]
    public async Task ListagemNaoDevolveCpfDeJeitoNenhum()
    {
        var cliente = fabrica.CreateClient();
        var recorte = Agora();
        await Cadastrar(cliente);

        var corpo = await cliente.GetStringAsync($"/propostas?de={recorte}&tamanho=50");

        Assert.DoesNotContain(Pedidos.CpfEmDigitos, corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("cpf", corpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CursorEstragadoEhPedidoInvalido()
    {
        var resposta = await fabrica.CreateClient().GetAsync("/propostas?cursor=nao-e-cursor");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task TamanhoForaDaFaixaEhPedidoInvalido(int tamanho)
    {
        var resposta = await fabrica.CreateClient().GetAsync($"/propostas?tamanho={tamanho}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task PeriodoInvertidoEhPedidoInvalido()
    {
        var agora = Agora();
        var resposta = await fabrica.CreateClient()
            .GetAsync($"/propostas?de={agora}&ate={Uri.EscapeDataString("2020-01-01T00:00:00.0000000+00:00")}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
