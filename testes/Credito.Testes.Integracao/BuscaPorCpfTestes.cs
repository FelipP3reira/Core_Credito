using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Credito.Dominio.Propostas;

namespace Credito.Testes.Integracao;

[Collection(ColecaoDaApi.Nome)]
public class BuscaPorCpfTestes
{
    private const int Permitidas = 3;

    private readonly FabricaDeApi fabrica;

    public BuscaPorCpfTestes(FabricaDeApi fabrica) => this.fabrica = fabrica;

    private sealed record Cadastrada(Guid Id, EstadoDaProposta Estado);

    private sealed record Linha(Guid Id, EstadoDaProposta Estado, string NomeSolicitante);

    private sealed record Pagina(IReadOnlyList<Linha> Itens, string? Proximo);

    private static async Task<Guid> Cadastrar(HttpClient cliente, string cpf)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = JsonContent.Create(Pedidos.Cadastro(cpf: cpf)),
        };
        pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());

        var resposta = await cliente.SendAsync(pedido);
        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json))!.Id;
    }

    private static Task<HttpResponseMessage> Buscar(HttpClient cliente, object corpo) =>
        cliente.PostAsync("/propostas/busca", JsonContent.Create(corpo));

    [Fact]
    public async Task AchaSoAsPropostasDoCpfProcurado()
    {
        var cliente = fabrica.CreateClient();
        var recorte = fabrica.Relogio.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);

        var procurada = await Cadastrar(cliente, Pedidos.CpfValido);
        await Cadastrar(cliente, Pedidos.OutroCpfValido);

        var resposta = await Buscar(cliente, new { cpf = Pedidos.CpfValido, de = recorte, tamanho = 50 });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var pagina = await resposta.Content.ReadFromJsonAsync<Pagina>(Pedidos.Json);

        Assert.Equal(procurada, Assert.Single(pagina!.Itens).Id);
    }

    // O numero enviado e formatado, o guardado e so digito: a busca tem que casar os dois.
    [Fact]
    public async Task AchaComOuSemPontuacao()
    {
        var cliente = fabrica.CreateClient();
        var recorte = fabrica.Relogio.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        var id = await Cadastrar(cliente, Pedidos.CpfValido);

        var resposta = await Buscar(cliente, new { cpf = Pedidos.CpfEmDigitos, de = recorte, tamanho = 50 });
        var pagina = await resposta.Content.ReadFromJsonAsync<Pagina>(Pedidos.Json);

        Assert.Equal(id, Assert.Single(pagina!.Itens).Id);
    }

    [Fact]
    public async Task RespostaDaBuscaNaoRepeteOCpf()
    {
        var cliente = fabrica.CreateClient();
        await Cadastrar(cliente, Pedidos.CpfValido);

        var corpo = await (await Buscar(cliente, new { cpf = Pedidos.CpfValido, tamanho = 50 }))
            .Content.ReadAsStringAsync();

        Assert.DoesNotContain(Pedidos.CpfEmDigitos, corpo, StringComparison.Ordinal);
        Assert.DoesNotContain("cpf", corpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CpfSemPropostaDevolveListaVazia()
    {
        var resposta = await Buscar(fabrica.CreateClient(), new { cpf = "398.213.404-82" });
        var pagina = await resposta.Content.ReadFromJsonAsync<Pagina>(Pedidos.Json);

        Assert.Empty(pagina!.Itens);
        Assert.Null(pagina.Proximo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("111.111.111-11")]
    [InlineData("529.982.247-26")]
    public async Task CpfInvalidoEhPedidoInvalido(string cpf) =>
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await Buscar(fabrica.CreateClient(), new { cpf })).StatusCode);

    /// <summary>
    /// O hash e deterministico: sem limite proprio, a rota responderia "existe proposta
    /// para este CPF?" quantas vezes quisessem, e isso e um enumerador.
    /// </summary>
    [Fact]
    public async Task BuscaTemLimiteProprioEMaisApertadoQueOCadastro()
    {
        using var api = new ApiComLimite(fabrica.StringDeConexao, buscas: Permitidas);
        var cliente = api.CreateClient();

        var codigos = new List<HttpStatusCode>();
        for (var tentativa = 0; tentativa < Permitidas + 2; tentativa++)
        {
            codigos.Add((await Buscar(cliente, new { cpf = Pedidos.CpfValido })).StatusCode);
        }

        Assert.Equal(Permitidas, codigos.Count(codigo => codigo == HttpStatusCode.OK));
        Assert.Equal(2, codigos.Count(codigo => codigo == HttpStatusCode.TooManyRequests));

        // O cadastro nao pode ter sido consumido junto: sao contadores separados.
        var cadastro = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = JsonContent.Create(Pedidos.Cadastro()),
        };
        cadastro.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());

        Assert.Equal(HttpStatusCode.Created, (await cliente.SendAsync(cadastro)).StatusCode);
    }
}
