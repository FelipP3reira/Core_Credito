using System.Net;
using System.Net.Http.Json;

namespace Credito.Testes.Integracao;

[Collection(ColecaoDaApi.Nome)]
public class LimiteDeSubmissaoTestes
{
    private const int Permitidas = 3;

    private readonly FabricaDeApi fabrica;

    public LimiteDeSubmissaoTestes(FabricaDeApi fabrica) => this.fabrica = fabrica;

    [Fact]
    public async Task RecusaOExcedenteEDizQuandoTentarDeNovo()
    {
        using var api = new ApiComLimite(fabrica.StringDeConexao, submissoes: Permitidas);
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
