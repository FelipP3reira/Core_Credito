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
    public async Task EnviaParaAnaliseEGuardaATransicaoNoHistorico()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        var analise = await cliente.PostAsync($"/propostas/{id}/analise", content: null);

        Assert.Equal(HttpStatusCode.Accepted, analise.StatusCode);

        var detalhe = await cliente.GetFromJsonAsync<Detalhe>($"/propostas/{id}", Pedidos.Json);

        Assert.Equal(EstadoDaProposta.EmAnalise, detalhe!.Estado);

        var transicao = Assert.Single(detalhe.Historico);
        Assert.Equal(EstadoDaProposta.Rascunho, transicao.De);
        Assert.Equal(EstadoDaProposta.EmAnalise, transicao.Para);
        Assert.Equal("api:submissao", transicao.Origem);
    }

    [Fact]
    public async Task RecusaMandarParaAnaliseDuasVezes()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        await cliente.PostAsync($"/propostas/{id}/analise", content: null);
        var repetida = await cliente.PostAsync($"/propostas/{id}/analise", content: null);

        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
    }

    [Fact]
    public async Task PropostaInexistenteDevolveNaoEncontrada()
    {
        var cliente = fabrica.CreateClient();
        var inexistente = Guid.NewGuid();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await cliente.GetAsync($"/propostas/{inexistente}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await cliente.PostAsync($"/propostas/{inexistente}/analise", content: null)).StatusCode);
    }

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
