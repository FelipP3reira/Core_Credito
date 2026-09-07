using System.Net;
using System.Net.Http.Json;
using Credito.Dominio.Decisoes.Regras;
using Credito.Dominio.Propostas;

namespace Credito.Testes.Integracao;

[Collection(ColecaoDaApi.Nome)]
public class AnaliseTestes
{
    private readonly FabricaDeApi fabrica;

    public AnaliseTestes(FabricaDeApi fabrica) => this.fabrica = fabrica;

    private sealed record Cadastrada(Guid Id, EstadoDaProposta Estado);

    private sealed record LinhaDoLaudo(
        string Codigo,
        bool Aprovou,
        string Motivo,
        decimal? ValorObservado,
        decimal? LimiteExigido);

    private sealed record Decisao(
        Guid PropostaId,
        EstadoDaProposta Estado,
        bool Aprovada,
        int ScoreObservado,
        decimal TaxaMensalAplicada,
        int VersaoDaPolitica,
        DateTimeOffset AvaliadaEm,
        IReadOnlyList<LinhaDoLaudo> Laudo);

    private sealed record Mudanca(EstadoDaProposta De, EstadoDaProposta Para, string Origem);

    private sealed record LaudoGuardado(
        bool Aprovada,
        int ScoreObservado,
        decimal TaxaMensalAplicada,
        int VersaoDaPolitica,
        IReadOnlyList<LinhaDoLaudo> Regras);

    private sealed record Detalhe(
        Guid Id,
        EstadoDaProposta Estado,
        IReadOnlyList<Mudanca> Historico,
        LaudoGuardado? Decisao);

    private static async Task<Guid> Cadastrar(
        HttpClient cliente,
        decimal valorSolicitado = 20_000m,
        int prazoEmMeses = 24,
        decimal rendaMensal = 8_500m)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = JsonContent.Create(Pedidos.Cadastro(
                valorSolicitado: valorSolicitado,
                rendaMensal: rendaMensal,
                prazoEmMeses: prazoEmMeses)),
        };
        pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());

        var resposta = await cliente.SendAsync(pedido);
        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json))!.Id;
    }

    private static Task<HttpResponseMessage> Analisar(HttpClient cliente, Guid id) =>
        cliente.PostAsync($"/propostas/{id}/analise", content: null);

    [Fact]
    public async Task AprovaQuandoTodasAsRegrasPassam()
    {
        fabrica.Bureau.Responder(score: 800);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        var resposta = await Analisar(cliente, id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var decisao = await resposta.Content.ReadFromJsonAsync<Decisao>(Pedidos.Json);

        Assert.True(decisao!.Aprovada);
        Assert.Equal(EstadoDaProposta.Aprovada, decisao.Estado);
        Assert.Equal(800, decisao.ScoreObservado);
        Assert.Equal(1, decisao.VersaoDaPolitica);
        Assert.All(decisao.Laudo, linha => Assert.True(linha.Aprovou));
    }

    /// <summary>
    /// Score 800 cai na faixa de 700 a 849, que a politica semeada cobra a 1,9% ao mes.
    /// A taxa nao vem de quem pergunta.
    /// </summary>
    [Fact]
    public async Task AplicaATaxaDaFaixaDeScoreDaPolitica()
    {
        var cliente = fabrica.CreateClient();

        fabrica.Bureau.Responder(score: 800);
        var comScoreBom = await Cadastrar(cliente);
        var primeira = await (await Analisar(cliente, comScoreBom)).Content.ReadFromJsonAsync<Decisao>(Pedidos.Json);

        fabrica.Bureau.Responder(score: 900);
        var comScoreOtimo = await Cadastrar(cliente);
        var segunda = await (await Analisar(cliente, comScoreOtimo)).Content.ReadFromJsonAsync<Decisao>(Pedidos.Json);

        Assert.Equal(0.019m, primeira!.TaxaMensalAplicada);
        Assert.Equal(0.012m, segunda!.TaxaMensalAplicada);
    }

    /// <summary>
    /// O teste que justifica o motor nao ter atalho: cinco regras reprovam e as cinco
    /// aparecem no laudo. Parar na primeira registraria uma causa quando existiam cinco.
    /// </summary>
    [Fact]
    public async Task RegistraTodasAsCausasDaNegativaDeUmaVez()
    {
        fabrica.Bureau.Responder(score: 300, temRestricao: true);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente, valorSolicitado: 150_000m, prazoEmMeses: 120, rendaMensal: 1_500m);

        var decisao = await (await Analisar(cliente, id)).Content.ReadFromJsonAsync<Decisao>(Pedidos.Json);

        Assert.False(decisao!.Aprovada);
        Assert.Equal(EstadoDaProposta.Negada, decisao.Estado);
        Assert.Equal(
            [
                RestricaoCadastral.CodigoDaRegra,
                ScoreMinimo.CodigoDaRegra,
                ValorDentroDoProduto.CodigoDaRegra,
                PrazoDentroDoProduto.CodigoDaRegra,
                ComprometimentoDeRenda.CodigoDaRegra,
            ],
            decisao.Laudo.Where(linha => !linha.Aprovou).Select(linha => linha.Codigo));
    }

    [Fact]
    public async Task RestricaoCadastralSozinhaJaNega()
    {
        fabrica.Bureau.Responder(score: 950, temRestricao: true);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        var decisao = await (await Analisar(cliente, id)).Content.ReadFromJsonAsync<Decisao>(Pedidos.Json);

        Assert.False(decisao!.Aprovada);
        Assert.Equal(
            [RestricaoCadastral.CodigoDaRegra],
            decisao.Laudo.Where(linha => !linha.Aprovou).Select(linha => linha.Codigo));
    }

    /// <summary>
    /// Score abaixo do minimo ainda recebe taxa — a da pior faixa — para que o cronograma
    /// possa ser montado e TODAS as regras sejam avaliadas. A proposta e negada pela regra
    /// de score, e nao por faltar taxa no meio da analise.
    /// </summary>
    [Fact]
    public async Task ScoreAbaixoDoMinimoAindaProduzLaudoCompleto()
    {
        fabrica.Bureau.Responder(score: 120);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        var decisao = await (await Analisar(cliente, id)).Content.ReadFromJsonAsync<Decisao>(Pedidos.Json);

        Assert.False(decisao!.Aprovada);
        Assert.Equal(0.049m, decisao.TaxaMensalAplicada);
        Assert.Equal(5, decisao.Laudo.Count);
        Assert.Single(decisao.Laudo, linha => !linha.Aprovou && linha.Codigo == ScoreMinimo.CodigoDaRegra);
    }

    [Fact]
    public async Task GuardaAsDuasTransicoesNoHistorico()
    {
        fabrica.Bureau.Responder(score: 800);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        await Analisar(cliente, id);

        var detalhe = await cliente.GetFromJsonAsync<Detalhe>($"/propostas/{id}", Pedidos.Json);

        Assert.Equal(
            [
                (EstadoDaProposta.Rascunho, EstadoDaProposta.EmAnalise, "api:submissao"),
                (EstadoDaProposta.EmAnalise, EstadoDaProposta.Aprovada, "motor:decisao"),
            ],
            detalhe!.Historico.Select(mudanca => (mudanca.De, mudanca.Para, mudanca.Origem)));
    }

    [Fact]
    public async Task OLaudoFicaGravadoEApareceNaConsulta()
    {
        fabrica.Bureau.Responder(score: 640);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        await Analisar(cliente, id);

        var detalhe = await cliente.GetFromJsonAsync<Detalhe>($"/propostas/{id}", Pedidos.Json);

        Assert.NotNull(detalhe!.Decisao);
        Assert.Equal(640, detalhe.Decisao.ScoreObservado);
        Assert.Equal(0.029m, detalhe.Decisao.TaxaMensalAplicada);
        Assert.Equal(1, detalhe.Decisao.VersaoDaPolitica);
        Assert.Equal(5, detalhe.Decisao.Regras.Count);
        Assert.All(detalhe.Decisao.Regras, regra => Assert.False(string.IsNullOrWhiteSpace(regra.Motivo)));
    }

    [Fact]
    public async Task PropostaSemAnaliseNaoTemLaudo()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        var detalhe = await cliente.GetFromJsonAsync<Detalhe>($"/propostas/{id}", Pedidos.Json);

        Assert.Null(detalhe!.Decisao);
    }

    [Fact]
    public async Task RecusaAnalisarDuasVezes()
    {
        fabrica.Bureau.Responder(score: 800);
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        await Analisar(cliente, id);
        var segunda = await Analisar(cliente, id);

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task PropostaInexistenteDevolveNaoEncontrada() =>
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await Analisar(fabrica.CreateClient(), Guid.NewGuid())).StatusCode);
}
