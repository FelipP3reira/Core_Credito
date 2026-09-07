using System.Net;
using System.Net.Http.Json;
using Credito.Dominio.Propostas;

namespace Credito.Testes.Integracao;

[Collection(ColecaoDaApi.Nome)]
public class CicloDaPropostaTestes
{
    private readonly FabricaDeApi fabrica;

    public CicloDaPropostaTestes(FabricaDeApi fabrica) => this.fabrica = fabrica;

    private sealed record Cadastrada(Guid Id, EstadoDaProposta Estado);

    private sealed record Mudanca(EstadoDaProposta De, EstadoDaProposta Para, string Origem);

    private sealed record Detalhe(
        Guid Id,
        EstadoDaProposta Estado,
        string NomeSolicitante,
        string Cpf,
        decimal ValorSolicitado,
        IReadOnlyList<Mudanca> Historico);

    private static async Task<Guid> Cadastrar(HttpClient cliente)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = JsonContent.Create(Pedidos.Cadastro()),
        };
        pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());

        var resposta = await cliente.SendAsync(pedido);
        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json))!.Id;
    }

    /// <summary>
    /// Confere que a transicao entra como linha nova. Com a chave declarada como gerada
    /// pelo banco, o EF transformava a transicao recem-criada em UPDATE, atingia zero
    /// linhas e derrubava a requisicao com erro de concorrencia.
    /// </summary>
    [Fact]
    public async Task ATransicaoDeEstadoEntraComoLinhaNova()
    {
        fabrica.Bureau.Responder(score: 800);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        var analise = await cliente.PostAsync($"/propostas/{id}/analise", content: null);

        Assert.Equal(HttpStatusCode.OK, analise.StatusCode);

        var detalhe = await cliente.GetFromJsonAsync<Detalhe>($"/propostas/{id}", Pedidos.Json);

        Assert.Equal(2, detalhe!.Historico.Count);
        Assert.Equal(EstadoDaProposta.Rascunho, detalhe.Historico[0].De);
    }

    [Fact]
    public async Task PropostaInexistenteDevolveNaoEncontrada() =>
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await fabrica.CreateClient().GetAsync($"/propostas/{Guid.NewGuid()}")).StatusCode);

    [Fact]
    public async Task DetalheMascaraOCpfENaoDevolveARenda()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        var bruto = await cliente.GetStringAsync($"/propostas/{id}");
        var detalhe = await cliente.GetFromJsonAsync<Detalhe>($"/propostas/{id}", Pedidos.Json);

        Assert.Equal("***.***.247-**", detalhe!.Cpf);
        Assert.DoesNotContain(Pedidos.CpfEmDigitos, bruto, StringComparison.Ordinal);
        Assert.DoesNotContain("rendaMensal", bruto, StringComparison.OrdinalIgnoreCase);
    }
}
