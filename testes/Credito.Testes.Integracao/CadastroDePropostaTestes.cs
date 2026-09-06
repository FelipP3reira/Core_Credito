using System.Net;
using System.Net.Http.Json;
using Credito.Dominio.Propostas;
using Credito.Infraestrutura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Credito.Testes.Integracao;

[Collection(ColecaoDaApi.Nome)]
public class CadastroDePropostaTestes
{
    private readonly FabricaDeApi fabrica;

    public CadastroDePropostaTestes(FabricaDeApi fabrica) => this.fabrica = fabrica;

    private HttpClient Cliente() => fabrica.CreateClient();

    private static HttpRequestMessage Envio(string chave, object corpo)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, "/propostas")
        {
            Content = JsonContent.Create(corpo),
        };
        pedido.Headers.Add("Idempotency-Key", chave);
        return pedido;
    }

    private sealed record Cadastrada(Guid Id, EstadoDaProposta Estado);

    [Fact]
    public async Task CadastraEmRascunhoEApontaOndeConsultar()
    {
        var resposta = await Cliente().SendAsync(Envio(Pedidos.ChaveNova(), Pedidos.Cadastro()));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var criada = await resposta.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json);
        Assert.NotNull(criada);
        Assert.Equal(EstadoDaProposta.Rascunho, criada.Estado);
        Assert.Equal($"/propostas/{criada.Id}", resposta.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ReenvioIdenticoDevolveAMesmaPropostaSemCriarOutra()
    {
        var chave = Pedidos.ChaveNova();
        var cliente = Cliente();

        var primeira = await cliente.SendAsync(Envio(chave, Pedidos.Cadastro()));
        var segunda = await cliente.SendAsync(Envio(chave, Pedidos.Cadastro()));

        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);

        var antes = await primeira.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json);
        var depois = await segunda.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json);
        Assert.Equal(antes!.Id, depois!.Id);

        Assert.Equal(1, await ContarComChave(chave));
    }

    // Devolver a proposta ja gravada aqui seria pior que falhar: o cliente receberia
    // 200 para dados que nunca entraram no sistema.
    [Fact]
    public async Task MesmaChaveComOutroConteudoRecusa()
    {
        var chave = Pedidos.ChaveNova();
        var cliente = Cliente();

        await cliente.SendAsync(Envio(chave, Pedidos.Cadastro(valorSolicitado: 20_000m)));
        var conflito = await cliente.SendAsync(Envio(chave, Pedidos.Cadastro(valorSolicitado: 31_000m)));

        Assert.Equal(HttpStatusCode.Conflict, conflito.StatusCode);
        Assert.Equal(1, await ContarComChave(chave));
    }

    /// <summary>
    /// A prova de que a idempotencia mora no indice unico. Uma checagem antes do insert
    /// passaria neste teste apenas por sorte de agendamento.
    /// </summary>
    [Fact]
    public async Task EnviosSimultaneosComAMesmaChaveCriamUmaPropostaSo()
    {
        var chave = Pedidos.ChaveNova();
        var cliente = Cliente();

        var respostas = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => cliente.SendAsync(Envio(chave, Pedidos.Cadastro()))));

        Assert.Equal(1, await ContarComChave(chave));
        Assert.Single(respostas, resposta => resposta.StatusCode == HttpStatusCode.Created);
        Assert.All(respostas, resposta =>
            Assert.Contains(resposta.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK }));

        var ids = new HashSet<Guid>();
        foreach (var resposta in respostas)
        {
            ids.Add((await resposta.Content.ReadFromJsonAsync<Cadastrada>(Pedidos.Json))!.Id);
        }

        Assert.Single(ids);
    }

    [Fact]
    public async Task RecusaSemOCabecalhoDeIdempotencia()
    {
        var resposta = await Cliente().PostAsJsonAsync("/propostas", Pedidos.Cadastro());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task RecusaCpfInvalidoDizendoQualCampoErrou()
    {
        var resposta = await Cliente().SendAsync(
            Envio(Pedidos.ChaveNova(), Pedidos.Cadastro(cpf: "111.111.111-11")));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("Cpf", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecusaSolicitanteMenorDeIdade()
    {
        var resposta = await Cliente().SendAsync(
            Envio(Pedidos.ChaveNova(), Pedidos.Cadastro(dataDeNascimento: "2015-03-12")));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(2_000_000)]
    public async Task RecusaValorForaDaFaixa(decimal valorSolicitado)
    {
        var resposta = await Cliente().SendAsync(
            Envio(Pedidos.ChaveNova(), Pedidos.Cadastro(valorSolicitado: valorSolicitado)));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    /// <summary>A garantia que o projeto inteiro existe para dar: CPF nunca em claro.</summary>
    [Fact]
    public async Task NaoGravaOCpfEmTextoClaroEmNenhumaColuna()
    {
        var chave = Pedidos.ChaveNova();
        await Cliente().SendAsync(Envio(chave, Pedidos.Cadastro()));

        using var escopo = fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<ContextoDeCredito>();
        var gravada = await contexto.Propostas.AsNoTracking()
            .FirstAsync(proposta => proposta.ChaveIdempotencia == chave);

        Assert.DoesNotContain(Pedidos.CpfEmDigitos, gravada.Cpf.Hash, StringComparison.Ordinal);
        Assert.DoesNotContain(Pedidos.CpfEmDigitos, gravada.Cpf.Cifrado, StringComparison.Ordinal);
        Assert.DoesNotContain(Pedidos.CpfEmDigitos, gravada.ImpressaoDoPedido, StringComparison.Ordinal);
        Assert.DoesNotContain(Pedidos.CpfEmDigitos, gravada.NomeSolicitante, StringComparison.Ordinal);
    }

    private async Task<int> ContarComChave(string chave)
    {
        using var escopo = fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<ContextoDeCredito>();

        return await contexto.Propostas.CountAsync(proposta => proposta.ChaveIdempotencia == chave);
    }
}
