using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Credito.Testes.Integracao;

/// <summary>
/// Segunda instancia da API sobre o MESMO container, com os limites por IP apertados.
/// </summary>
/// <remarks>
/// Subir outro SQL Server para provar um contador em memoria nao se paga. Fabrica
/// propria, e nao um ajuste em cima da existente, porque ai a ordem entre os provedores
/// de configuracao decide quem vence — e ela nao e obvia o bastante para um teste
/// depender dela.
/// </remarks>
internal sealed class ApiComLimite : WebApplicationFactory<Program>
{
    private const int SemLimitePratico = 10_000;

    private readonly string stringDeConexao;
    private readonly int submissoes;
    private readonly int buscas;

    public ApiComLimite(
        string stringDeConexao,
        int submissoes = SemLimitePratico,
        int buscas = SemLimitePratico)
    {
        this.stringDeConexao = stringDeConexao;
        this.submissoes = submissoes;
        this.buscas = buscas;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.AplicarConfiguracaoDeTeste(stringDeConexao, submissoes, buscas);
}
