using System.Net;
using System.Net.Http.Json;
using Credito.Dominio.Propostas;

namespace Credito.Testes.Integracao;

[Collection(ColecaoDaApi.Nome)]
public class CancelamentoTestes
{
    private readonly FabricaDeApi fabrica;

    public CancelamentoTestes(FabricaDeApi fabrica) => this.fabrica = fabrica;

    private sealed record Cadastrada(Guid Id, EstadoDaProposta Estado);

    private sealed record Mudanca(int Sequencia, EstadoDaProposta De, EstadoDaProposta Para, string Origem);

    private sealed record Detalhe(Guid Id, EstadoDaProposta Estado, IReadOnlyList<Mudanca> Historico);

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

    private static Task<HttpResponseMessage> Cancelar(HttpClient cliente, Guid id) =>
        cliente.PostAsync($"/propostas/{id}/cancelamento", content: null);

    [Fact]
    public async Task CancelaPropostaEmRascunhoEDeixaRastro()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        var resposta = await Cancelar(cliente, id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var detalhe = await cliente.GetFromJsonAsync<Detalhe>($"/propostas/{id}", Pedidos.Json);

        Assert.Equal(EstadoDaProposta.Cancelada, detalhe!.Estado);
        var mudanca = Assert.Single(detalhe.Historico);
        Assert.Equal(EstadoDaProposta.Cancelada, mudanca.Para);
        Assert.Equal("api:cancelamento", mudanca.Origem);
    }

    [Fact]
    public async Task CancelarDuasVezesEhRecusado()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        await Cancelar(cliente, id);

        Assert.Equal(HttpStatusCode.Conflict, (await Cancelar(cliente, id)).StatusCode);
    }

    // Depois de decidida existe laudo, e cancelar apagaria o motivo de uma decisao tomada.
    [Fact]
    public async Task PropostaJaAprovadaNaoCancela()
    {
        fabrica.Bureau.Responder(score: 800);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);
        await cliente.PostAsync($"/propostas/{id}/analise", content: null);

        Assert.Equal(HttpStatusCode.Conflict, (await Cancelar(cliente, id)).StatusCode);
    }

    [Fact]
    public async Task PropostaCanceladaNaoEntraEmAnalise()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);
        await Cancelar(cliente, id);

        var analise = await cliente.PostAsync($"/propostas/{id}/analise", content: null);

        Assert.Equal(HttpStatusCode.Conflict, analise.StatusCode);
    }

    [Fact]
    public async Task PropostaInexistenteDevolveNaoEncontrada() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Cancelar(fabrica.CreateClient(), Guid.NewGuid())).StatusCode);
}
