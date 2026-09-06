using System.Threading.RateLimiting;
using Credito.Api.Erros;
using Credito.Api.Propostas;
using Credito.Aplicacao.Portas;
using Credito.Aplicacao.Propostas;
using Credito.Infraestrutura.Persistencia;
using Credito.Infraestrutura.Seguranca;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Credito.Api.Configuracao;

internal static class ServicosDeCredito
{
    public static IServiceCollection AdicionarCredito(this IServiceCollection servicos, IConfiguration configuracao)
    {
        var conexao = configuracao.GetConnectionString("Banco")
            ?? throw new InvalidOperationException("ConnectionStrings:Banco nao configurada. Veja o .env.example.");

        servicos.AddDbContext<ContextoDeCredito>(opcoes => opcoes.UseSqlServer(conexao));

        servicos.Configure<OpcoesDeProtecaoDeCpf>(configuracao.GetSection(OpcoesDeProtecaoDeCpf.Secao));
        servicos.Configure<LimiteDeSubmissao>(configuracao.GetSection(LimiteDeSubmissao.Secao));

        servicos.AddSingleton(TimeProvider.System);
        servicos.AddSingleton<IProtetorDeCpf, ProtetorDeCpf>();
        servicos.AddScoped<IRepositorioDePropostas, RepositorioDePropostas>();

        servicos.AddScoped<CadastrarProposta>();
        servicos.AddScoped<EnviarPropostaParaAnalise>();
        servicos.AddScoped<ConsultarProposta>();

        servicos.AddScoped<IValidator<PedidoDeCadastro>, ValidadorDeCadastro>();

        servicos.AddProblemDetails();
        servicos.AddExceptionHandler<TratamentoDeErrosDeDominio>();

        return servicos.AdicionarLimiteDeSubmissao(configuracao);
    }

    private static IServiceCollection AdicionarLimiteDeSubmissao(
        this IServiceCollection servicos,
        IConfiguration configuracao)
    {
        var limite = configuracao.GetSection(LimiteDeSubmissao.Secao).Get<LimiteDeSubmissao>() ?? new LimiteDeSubmissao();

        return servicos.AddRateLimiter(limitador =>
        {
            limitador.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limitador.AddPolicy(EndpointsDePropostas.PoliticaDeLimite, contexto =>
                RateLimitPartition.GetFixedWindowLimiter(
                    Identificar(contexto),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limite.Permitidas,
                        Window = TimeSpan.FromSeconds(limite.JanelaEmSegundos),

                        // Fila zero: quem passou do limite recebe 429 na hora. Enfileirar
                        // so empurraria a espera para o cliente sem aliviar o servidor.
                        QueueLimit = 0,
                    }));

            limitador.OnRejected = async (contexto, cancelamento) =>
            {
                if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                {
                    contexto.HttpContext.Response.Headers.RetryAfter =
                        ((int)espera.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await contexto.HttpContext.Response
                    .WriteAsJsonAsync(
                        new { titulo = "Pedidos demais", detalhe = "Espere um pouco antes de enviar outra proposta." },
                        cancelamento)
                    .ConfigureAwait(false);
            };
        });
    }

    /// <remarks>
    /// Particiona por IP de conexao. Atras de proxy ou balanceador isso vira o IP do
    /// proxy e o limite passa a valer para todo mundo junto — nesse caso e preciso
    /// ligar ForwardedHeaders antes, com a lista de proxies confiaveis.
    /// </remarks>
    private static string Identificar(HttpContext contexto) =>
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
}
