using System.Net;
using System.Net.Http.Json;
using Credito.Dominio.Propostas;

namespace Credito.Testes.Integracao;

[Collection(ColecaoDaApi.Nome)]
public class AuditoriaTestes
{
    private const int Prazo = 6;

    private readonly FabricaDeApi fabrica;

    public AuditoriaTestes(FabricaDeApi fabrica) => this.fabrica = fabrica;

    private sealed record Cadastrada(Guid Id, EstadoDaProposta Estado);

    private sealed record Evento(DateTimeOffset OcorridoEm, string Tipo, string Resumo, string Origem);

    private sealed record Trilha(
        Guid PropostaId,
        EstadoDaProposta Estado,
        string NomeSolicitante,
        string Cpf,
        IReadOnlyList<Evento> Eventos);

    private static async Task<Guid> Cadastrar(HttpClient cliente)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = JsonContent.Create(Pedidos.Cadastro(valorSolicitado: 12_000m, prazoEmMeses: Prazo)),
        };
        pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());

        var resposta = await cliente.SendAsync(pedido);
        resposta.EnsureSuccessStatusCode();

        return (await resposta.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json))!.Id;
    }

    private static Task<Trilha?> Auditar(HttpClient cliente, Guid id) =>
        cliente.GetFromJsonAsync<Trilha>($"/propostas/{id}/auditoria", Pedidos.Json);

    private async Task<Guid> CicloCompleto(HttpClient cliente)
    {
        fabrica.Bureau.Responder(score: 820);

        var id = await Cadastrar(cliente);
        (await cliente.PostAsync($"/propostas/{id}/analise", content: null)).EnsureSuccessStatusCode();
        (await cliente.PostAsync($"/propostas/{id}/contrato", content: null)).EnsureSuccessStatusCode();

        for (var numero = 1; numero <= Prazo; numero++)
        {
            var pagamento = new HttpRequestMessage(
                HttpMethod.Post,
                $"/propostas/{id}/contrato/parcelas/{numero}/pagamento");
            pagamento.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());

            (await cliente.SendAsync(pagamento)).EnsureSuccessStatusCode();
        }

        return id;
    }

    [Fact]
    public async Task PropostaRecemCadastradaNaoTemEventoNenhum()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        var trilha = await Auditar(cliente, id);

        Assert.Equal(EstadoDaProposta.Rascunho, trilha!.Estado);
        Assert.Empty(trilha.Eventos);
    }

    /// <summary>
    /// A analise faz duas transicoes e grava o laudo no mesmo instante. A trilha tem que
    /// sair na ordem em que as coisas aconteceram, e nao na ordem que o relogio permite
    /// distinguir.
    /// </summary>
    [Fact]
    public async Task LaudoVemLogoDepoisDaTransicaoQueEleJustificou()
    {
        var cliente = fabrica.CreateClient();
        fabrica.Bureau.Responder(score: 820);

        var id = await Cadastrar(cliente);
        await cliente.PostAsync($"/propostas/{id}/analise", content: null);

        var eventos = (await Auditar(cliente, id))!.Eventos;

        Assert.Equal("estado", eventos[0].Tipo);
        Assert.Equal("Rascunho para EmAnalise", eventos[0].Resumo);
        Assert.Equal("estado", eventos[1].Tipo);
        Assert.Equal("EmAnalise para Aprovada", eventos[1].Resumo);
        Assert.Equal("decisao", eventos[2].Tipo);
        Assert.All(eventos.Skip(3), evento => Assert.Equal("regra", evento.Tipo));
    }

    // Sem as regras que passaram nao da para saber o que chegou a ser conferido.
    [Fact]
    public async Task RegistraTambemAsRegrasQuePassaram()
    {
        var cliente = fabrica.CreateClient();
        fabrica.Bureau.Responder(score: 820);

        var id = await Cadastrar(cliente);
        await cliente.PostAsync($"/propostas/{id}/analise", content: null);

        var regras = (await Auditar(cliente, id))!.Eventos.Where(evento => evento.Tipo == "regra").ToList();

        Assert.Equal(5, regras.Count);
        Assert.All(regras, regra => Assert.Contains("passou", regra.Resumo, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CicloInteiroSaiEmOrdemComContratoEPagamentos()
    {
        var cliente = fabrica.CreateClient();
        var id = await CicloCompleto(cliente);

        var trilha = await Auditar(cliente, id);

        Assert.Equal(EstadoDaProposta.Liquidada, trilha!.Estado);

        Assert.Equal(
            [
                "Rascunho para EmAnalise",
                "EmAnalise para Aprovada",
                "Aprovada para Contratada",
                "Contratada para Liquidada",
            ],
            trilha.Eventos.Where(evento => evento.Tipo == "estado").Select(evento => evento.Resumo));

        Assert.Single(trilha.Eventos, evento => evento.Tipo == "contrato");
        Assert.Equal(Prazo, trilha.Eventos.Count(evento => evento.Tipo == "pagamento"));

        // Nada pode aparecer fora de ordem cronologica.
        var instantes = trilha.Eventos.Select(evento => evento.OcorridoEm).ToList();
        Assert.Equal(instantes.Order(), instantes);
    }

    /// <summary>
    /// O ultimo pagamento e a liquidacao acontecem na mesma requisicao, com o mesmo
    /// instante. A causa tem que aparecer antes do efeito: sem desempate, a trilha mostra
    /// a proposta sendo liquidada antes do pagamento que a liquidou.
    /// </summary>
    [Fact]
    public async Task OPagamentoQueLiquidouVemAntesDaMudancaDeEstado()
    {
        var cliente = fabrica.CreateClient();
        var id = await CicloCompleto(cliente);

        var eventos = (await Auditar(cliente, id))!.Eventos;

        Assert.Equal("pagamento", eventos[^2].Tipo);
        Assert.Contains($"Parcela {Prazo} de {Prazo} paga", eventos[^2].Resumo, StringComparison.Ordinal);

        Assert.Equal("estado", eventos[^1].Tipo);
        Assert.Equal("Contratada para Liquidada", eventos[^1].Resumo);

        Assert.Equal(eventos[^2].OcorridoEm, eventos[^1].OcorridoEm);
    }

    // Mesmo caso na contratacao: assina-se o contrato e so entao a proposta muda de estado.
    [Fact]
    public async Task OContratoVemAntesDaTransicaoParaContratada()
    {
        var cliente = fabrica.CreateClient();
        fabrica.Bureau.Responder(score: 820);

        var id = await Cadastrar(cliente);
        await cliente.PostAsync($"/propostas/{id}/analise", content: null);
        await cliente.PostAsync($"/propostas/{id}/contrato", content: null);

        var eventos = (await Auditar(cliente, id))!.Eventos;
        var contrato = eventos.Single(evento => evento.Tipo == "contrato");

        Assert.Equal(contrato, eventos[^2]);
        Assert.Equal("Aprovada para Contratada", eventos[^1].Resumo);
    }

    [Fact]
    public async Task TrilhaMostraOCpfMascaradoENuncaONumero()
    {
        var cliente = fabrica.CreateClient();
        var id = await Cadastrar(cliente);

        var corpo = await cliente.GetStringAsync($"/propostas/{id}/auditoria");
        var trilha = await Auditar(cliente, id);

        Assert.Equal("***.***.247-**", trilha!.Cpf);
        Assert.DoesNotContain(Pedidos.CpfEmDigitos, corpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PropostaInexistenteDevolveNaoEncontrada()
    {
        var resposta = await fabrica.CreateClient().GetAsync($"/propostas/{Guid.NewGuid()}/auditoria");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
