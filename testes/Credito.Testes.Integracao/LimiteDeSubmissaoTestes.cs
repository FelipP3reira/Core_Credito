using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Credito.Testes.Integracao;

[Collection(ColecaoDaApi.Nome)]
public class LimiteDeSubmissaoTestes
{
    private const int Permitidas = 3;

    private readonly FabricaDeApi fabrica;

    public LimiteDeSubmissaoTestes(FabricaDeApi fabrica) => this.fabrica = fabrica;

    /// <summary>
    /// Segunda instancia da API sobre o MESMO container: subir outro SQL Server para
    /// provar um contador em memoria nao se paga. Fabrica propria, e nao um ajuste em
    /// cima da existente, porque ai a ordem entre os provedores de configuracao decide
    /// quem vence — e ela nao e obvia o bastante para um teste depender dela.
    /// </summary>
    private sealed class ApiComLimiteBaixo : WebApplicationFactory<Program>
    {
        private readonly string stringDeConexao;

        public ApiComLimiteBaixo(string stringDeConexao) => this.stringDeConexao = stringDeConexao;

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.AplicarConfiguracaoDeTeste(stringDeConexao, Permitidas);
    }

    [Fact]
    public async Task RecusaOExcedenteEDizQuandoTentarDeNovo()
    {
        using var api = new ApiComLimiteBaixo(fabrica.StringDeConexao);
        var cliente = api.CreateClient();

        var codigos = new List<HttpStatusCode>();
        HttpResponseMessage? recusada = null;

        for (var tentativa = 0; tentativa < Permitidas + 3; tentativa++)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Post, "/propostas")
            {
                Content = JsonContent.Create(Pedidos.Cadastro()),
            };
            pedido.Headers.Add("Idempotency-Key", Pedidos.ChaveNova());

            var resposta = await cliente.SendAsync(pedido);
            codigos.Add(resposta.StatusCode);
            recusada ??= resposta.StatusCode == HttpStatusCode.TooManyRequests ? resposta : null;
        }

        Assert.Equal(Permitidas, codigos.Count(codigo => codigo == HttpStatusCode.Created));
        Assert.Equal(3, codigos.Count(codigo => codigo == HttpStatusCode.TooManyRequests));

        Assert.NotNull(recusada);
        Assert.NotNull(recusada.Headers.RetryAfter);
    }
}
