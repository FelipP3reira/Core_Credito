using System.Globalization;
using System.Security.Cryptography;
using Credito.Aplicacao.Portas;
using Credito.Infraestrutura.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Credito.Testes.Integracao;

/// <summary>
/// Sobe a API contra um SQL Server de verdade em container.
/// </summary>
/// <remarks>
/// Banco real, e nao em memoria, porque metade do que estes testes provam so existe
/// no banco: o indice unico que resolve a idempotencia, o rowversion que barra escrita
/// concorrente e a escolha entre INSERT e UPDATE que o EF faz sozinho.
/// </remarks>
public sealed class FabricaDeApi : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Alto de proposito: so o teste do proprio limite quer ser barrado.</summary>
    private const int SubmissoesPermitidas = 10_000;

    // Mesma imagem do docker-compose: teste que roda contra versao diferente da que
    // vai para producao nao prova o que promete.
    private readonly MsSqlContainer banco =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string StringDeConexao => banco.GetConnectionString();

    /// <summary>
    /// Resposta do birô, ditada pelo teste. Compartilhada entre as classes da colecao, que
    /// o xUnit roda em sequencia — cada teste que se importa com score ajusta antes de agir.
    /// </summary>
    public BureauControlado Bureau { get; } = new();

    /// <summary>
    /// Relogio da API. Comeca na hora de verdade; o teste que precisa de tempo passando
    /// avanca e reinicia depois, ja que o xUnit roda a colecao em sequencia.
    /// </summary>
    public RelogioControlado Relogio { get; } = new();

    public async Task InitializeAsync()
    {
        await banco.StartAsync();

        using var escopo = Services.CreateScope();
        await escopo.ServiceProvider.GetRequiredService<ContextoDeCredito>().Database.MigrateAsync();
    }

    // WebApplicationFactory ja expoe DisposeAsync devolvendo ValueTask e o xUnit exige
    // Task. Implementacao explicita e o que faz as duas assinaturas conviverem.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await banco.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AplicarConfiguracaoDeTeste(StringDeConexao, SubmissoesPermitidas);
        builder.ConfigureTestServices(servicos =>
        {
            servicos.AddSingleton<IConsultaDeBureau>(Bureau);
            servicos.AddSingleton<TimeProvider>(Relogio);
        });
    }
}

internal static class ConfiguracaoDeTeste
{
    // Entra por ultimo para vencer o .env que o Program carrega na subida.
    public static void AplicarConfiguracaoDeTeste(
        this IWebHostBuilder builder,
        string stringDeConexao,
        int submissoesPermitidas) =>
        builder.ConfigureAppConfiguration(configuracao => configuracao.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Banco"] = stringDeConexao,
                ["ProtecaoDeCpf:Pepper"] = "pepper-de-teste",
                ["ProtecaoDeCpf:ChaveDeCifra"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["LimiteDeSubmissao:Permitidas"] =
                    submissoesPermitidas.ToString(CultureInfo.InvariantCulture),
                ["LimiteDeSubmissao:JanelaEmSegundos"] = "60",
            }));
}
