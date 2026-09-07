using System.Net;
using System.Net.Http.Json;

namespace Credito.Testes.Integracao;

/// <summary>
/// O emprestimo como produto da conta: o valor financiado entra na conta e cada parcela
/// paga sai dela.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class DesembolsoTestes
{
    private readonly FabricaDeApi fabrica;

    public DesembolsoTestes(FabricaDeApi fabrica)
    {
        this.fabrica = fabrica;

        // O dublê e compartilhado pela colecao, que o xUnit roda em sequencia: cada teste
        // comeca de um banco vazio em vez de herdar o saldo do anterior.
        fabrica.Banco.Reiniciar();
    }

    private sealed record Cadastrada(Guid Id);

    private sealed record Contrato(Guid Id, Guid? ContaId, decimal ValorFinanciado, DateTimeOffset? DesembolsadoEm);

    private sealed record Desembolso(
        Guid ContratoId,
        Guid ContaId,
        decimal Valor,
        Guid LancamentoId,
        decimal SaldoDaConta,
        bool Novo);

    private sealed record Pagamento(
        int Numero,
        decimal Valor,
        bool ContratoQuitado,
        Guid? LancamentoNaConta,
        decimal? SaldoDaConta);

    private sealed record ParcelaDoContrato(int Numero, decimal Valor, DateTimeOffset? PagaEm);

    private sealed record DetalheDoContrato(IReadOnlyList<ParcelaDoContrato> Parcelas);

    private async Task<Guid> Aprovada(HttpClient cliente)
    {
        fabrica.Bureau.Responder(score: 800);

        var pedido = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = JsonContent.Create(Pedidos.Cadastro(valorSolicitado: 10_000m, prazoEmMeses: 6)),
        };
        pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());

        var resposta = await cliente.SendAsync(pedido);
        resposta.EnsureSuccessStatusCode();

        var id = (await resposta.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json))!.Id;
        (await cliente.PostAsync($"/propostas/{id}/analise", content: null)).EnsureSuccessStatusCode();

        return id;
    }

    private async Task<(Guid Proposta, Guid Conta, Contrato Contrato)> Contratada(HttpClient cliente)
    {
        var proposta = await Aprovada(cliente);
        var conta = Guid.CreateVersion7();

        var resposta = await cliente.PostAsJsonAsync(
            $"/propostas/{proposta}/contrato",
            new { contaId = conta });
        resposta.EnsureSuccessStatusCode();

        return (proposta, conta, (await resposta.Content.ReadFromJsonAsync<Contrato>(Pedidos.Json))!);
    }

    private static Task<HttpResponseMessage> Desembolsar(HttpClient cliente, Guid proposta) =>
        cliente.PostAsync($"/propostas/{proposta}/contrato/desembolso", content: null);

    private static Task<HttpResponseMessage> Cobrar(
        HttpClient cliente,
        Guid proposta,
        int numero,
        string chave)
    {
        var pedido = new HttpRequestMessage(
            HttpMethod.Post,
            $"/propostas/{proposta}/contrato/parcelas/{numero}/pagamento");

        pedido.Headers.Add("Idempotency-Key", chave);

        return cliente.SendAsync(pedido);
    }

    [Fact]
    public async Task OValorFinanciadoEntraNaConta()
    {
        var cliente = fabrica.CreateClient();
        var (proposta, conta, contrato) = await Contratada(cliente);

        var resposta = await Desembolsar(cliente, proposta);
        var desembolso = await resposta.Content.ReadFromJsonAsync<Desembolso>(Pedidos.Json);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.True(desembolso!.Novo);
        Assert.Equal(conta, desembolso.ContaId);
        Assert.Equal(contrato.ValorFinanciado, desembolso.Valor);
        Assert.Equal(contrato.ValorFinanciado, desembolso.SaldoDaConta);
        Assert.Equal(contrato.ValorFinanciado, fabrica.Banco.SaldoDe(conta));
    }

    /// <summary>
    /// O caso que a chave derivada existe para resolver: quem repete o desembolso —
    /// por tempo esgotado, por nova tentativa, por dedo — nao recebe o emprestimo duas vezes.
    /// </summary>
    [Fact]
    public async Task DesembolsarDeNovoNaoCreditaSegundaVez()
    {
        var cliente = fabrica.CreateClient();
        var (proposta, conta, contrato) = await Contratada(cliente);

        await Desembolsar(cliente, proposta);
        var segunda = await Desembolsar(cliente, proposta);
        var desembolso = await segunda.Content.ReadFromJsonAsync<Desembolso>(Pedidos.Json);

        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        Assert.False(desembolso!.Novo);
        Assert.Equal(contrato.ValorFinanciado, fabrica.Banco.SaldoDe(conta));
    }

    [Fact]
    public async Task ODesembolsoFicaGravadoNoContrato()
    {
        var cliente = fabrica.CreateClient();
        var (proposta, conta, _) = await Contratada(cliente);

        await Desembolsar(cliente, proposta);

        var contrato = await cliente.GetFromJsonAsync<Contrato>(
            $"/propostas/{proposta}/contrato",
            Pedidos.Json);

        Assert.Equal(conta, contrato!.ContaId);
        Assert.NotNull(contrato.DesembolsadoEm);
    }

    [Fact]
    public async Task ContratoSemContaNaoTemParaOndeDesembolsar()
    {
        var cliente = fabrica.CreateClient();
        var proposta = await Aprovada(cliente);
        (await cliente.PostAsJsonAsync($"/propostas/{proposta}/contrato", new { })).EnsureSuccessStatusCode();

        var resposta = await Desembolsar(cliente, proposta);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(0, fabrica.Banco.Chamadas);
    }

    [Fact]
    public async Task AParcelaPagaSaiDaConta()
    {
        var cliente = fabrica.CreateClient();
        var (proposta, conta, contrato) = await Contratada(cliente);
        await Desembolsar(cliente, proposta);

        var resposta = await Cobrar(cliente, proposta, 1, Pedidos.ChaveNova());
        var pagamento = await resposta.Content.ReadFromJsonAsync<Pagamento>(Pedidos.Json);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.NotNull(pagamento!.LancamentoNaConta);
        Assert.Equal(contrato.ValorFinanciado - pagamento.Valor, pagamento.SaldoDaConta);
        Assert.Equal(pagamento.SaldoDaConta, fabrica.Banco.SaldoDe(conta));
    }

    /// <summary>
    /// Reenvio da mesma cobranca: a parcela ja esta paga e o banco reconhece a chave
    /// derivada. Sem isso, quem repete a cobranca cobra o cliente duas vezes.
    /// </summary>
    [Fact]
    public async Task ReenviarACobrancaNaoDebitaDuasVezes()
    {
        var cliente = fabrica.CreateClient();
        var (proposta, conta, contrato) = await Contratada(cliente);
        await Desembolsar(cliente, proposta);

        var chave = Pedidos.ChaveNova();
        var primeira = await (await Cobrar(cliente, proposta, 1, chave))
            .Content.ReadFromJsonAsync<Pagamento>(Pedidos.Json);
        await Cobrar(cliente, proposta, 1, chave);

        Assert.Equal(contrato.ValorFinanciado - primeira!.Valor, fabrica.Banco.SaldoDe(conta));
    }

    /// <summary>
    /// O banco recusa o debito e a parcela continua em aberto. O que garante isso e a
    /// gravacao acontecer depois da confirmacao do banco: salvar antes de cobrar quitaria a
    /// parcela sem ninguem ter pago.
    /// </summary>
    [Fact]
    public async Task ParcelaRecusadaPeloBancoContinuaEmAberto()
    {
        var cliente = fabrica.CreateClient();
        var (proposta, _, _) = await Contratada(cliente);
        await Desembolsar(cliente, proposta);

        fabrica.Banco.Recusa = "Saldo insuficiente.";
        var resposta = await Cobrar(cliente, proposta, 1, Pedidos.ChaveNova());

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        Assert.Null(await ParcelaPagaEm(cliente, proposta, numero: 1));
    }

    /// <summary>
    /// Banco fora do ar e falha, e nao recusa: 502 diz a quem chama que repetir faz sentido.
    /// </summary>
    [Fact]
    public async Task BancoForaDoArNaoQuitaAParcela()
    {
        var cliente = fabrica.CreateClient();
        var (proposta, _, _) = await Contratada(cliente);
        await Desembolsar(cliente, proposta);

        fabrica.Banco.ForaDoAr = true;
        var resposta = await Cobrar(cliente, proposta, 1, Pedidos.ChaveNova());

        fabrica.Banco.ForaDoAr = false;
        Assert.Equal(HttpStatusCode.BadGateway, resposta.StatusCode);
        Assert.Null(await ParcelaPagaEm(cliente, proposta, numero: 1));
    }

    /// <summary>
    /// Cobrar parcela de um contrato que tem conta e nunca desembolsou seria cobrar por um
    /// emprestimo que o cliente nao recebeu.
    /// </summary>
    [Fact]
    public async Task ContratoComContaNaoDesembolsadoNaoCobraParcela()
    {
        var cliente = fabrica.CreateClient();
        var (proposta, _, _) = await Contratada(cliente);

        var resposta = await Cobrar(cliente, proposta, 1, Pedidos.ChaveNova());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(0, fabrica.Banco.Chamadas);
    }

    /// <summary>
    /// Contrato sem conta continua funcionando como antes: a parcela e liquidada por fora e
    /// o credito so registra que foi paga.
    /// </summary>
    [Fact]
    public async Task ContratoSemContaPagaSemTocarNoBanco()
    {
        var cliente = fabrica.CreateClient();
        var proposta = await Aprovada(cliente);
        (await cliente.PostAsJsonAsync($"/propostas/{proposta}/contrato", new { })).EnsureSuccessStatusCode();

        var resposta = await Cobrar(cliente, proposta, 1, Pedidos.ChaveNova());
        var pagamento = await resposta.Content.ReadFromJsonAsync<Pagamento>(Pedidos.Json);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Null(pagamento!.LancamentoNaConta);
        Assert.Null(pagamento.SaldoDaConta);
        Assert.Equal(0, fabrica.Banco.Chamadas);
    }

    /// <summary>
    /// O ciclo inteiro: entra o emprestimo, saem todas as parcelas, e a conta zera.
    /// </summary>
    [Fact]
    public async Task OCicloInteiroPassaPelaConta()
    {
        var cliente = fabrica.CreateClient();
        var (proposta, conta, contrato) = await Contratada(cliente);
        await Desembolsar(cliente, proposta);

        var parcelas = (await cliente.GetFromJsonAsync<DetalheDoContrato>(
            $"/propostas/{proposta}/contrato",
            Pedidos.Json))!.Parcelas;

        // O cliente devolve mais do que recebeu: os juros saem do bolso dele. Sem este
        // aporte a ultima parcela e recusada por saldo — que e o que aconteceria na vida
        // real com quem gastou o emprestimo inteiro.
        var juros = parcelas.Sum(parcela => parcela.Valor) - contrato.ValorFinanciado;
        fabrica.Banco.Depositar(conta, juros);

        foreach (var parcela in parcelas)
        {
            (await Cobrar(cliente, proposta, parcela.Numero, Pedidos.ChaveNova()))
                .EnsureSuccessStatusCode();
        }

        Assert.Equal(0m, fabrica.Banco.SaldoDe(conta));
    }

    private static async Task<DateTimeOffset?> ParcelaPagaEm(HttpClient cliente, Guid proposta, int numero)
    {
        var contrato = await cliente.GetFromJsonAsync<DetalheDoContrato>(
            $"/propostas/{proposta}/contrato",
            Pedidos.Json);

        return contrato!.Parcelas.First(parcela => parcela.Numero == numero).PagaEm;
    }
}
