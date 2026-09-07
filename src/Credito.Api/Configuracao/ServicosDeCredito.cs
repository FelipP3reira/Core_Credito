using System.Threading.RateLimiting;
using Credito.Api.Erros;
using Credito.Api.Propostas;
using Credito.Aplicacao.Analises;
using Credito.Aplicacao.Contratos;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Amortizacao;
using Credito.Dominio.Decisoes;
using Credito.Dominio.Decisoes.Regras;
using Credito.Aplicacao.Propostas;
using Credito.Infraestrutura.Bureau;
using Credito.Infraestrutura.Persistencia;
using Credito.Infraestrutura.Seguranca;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Credito.Api.Configuracao;

internal static class ServicosDeCredito
{
    public static IServiceCollection AdicionarCredito(this IServiceCollection servicos, IConfiguration configuracao)
    {
        // A string de conexao e lida do provedor, nao capturada aqui. Configuracao lida
        // no momento do registro congela o valor que existia antes de as fontes
        // adicionadas depois entrarem — e e exatamente isso que a fabrica dos testes de
        // integracao faz para apontar a API ao banco em container.
        servicos.AddDbContext<ContextoDeCredito>((provedor, opcoes) =>
            opcoes.UseSqlServer(
                provedor.GetRequiredService<IConfiguration>().GetConnectionString("Banco")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:Banco nao configurada. Veja o .env.example.")));

        servicos.Configure<OpcoesDeProtecaoDeCpf>(configuracao.GetSection(OpcoesDeProtecaoDeCpf.Secao));
        servicos.Configure<LimiteDeSubmissao>(configuracao.GetSection(LimiteDeSubmissao.Secao));

        servicos.AddSingleton(TimeProvider.System);
        servicos.AddSingleton<IProtetorDeCpf, ProtetorDeCpf>();
        servicos.AddScoped<IRepositorioDePropostas, RepositorioDePropostas>();

        servicos.AddScoped<CadastrarProposta>();
        servicos.AddScoped<ConsultarProposta>();
        servicos.AddScoped<SimularProposta>();
        servicos.AddScoped<AnalisarProposta>();
        servicos.AddScoped<MontadorDoContexto>();

        servicos.AddScoped<IRepositorioDePoliticas, RepositorioDePoliticas>();
        servicos.AddScoped<IRepositorioDeContratos, RepositorioDeContratos>();
        servicos.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();

        servicos.AddScoped<ContratarProposta>();
        servicos.AddScoped<RegistrarPagamento>();
        servicos.AddScoped<ConsultarContrato>();
        servicos.AddSingleton<IConsultaDeBureau, BureauSimulado>();

        // Registrados pela interface para o resolvedor receber todos de uma vez: sistema
        // novo entra aqui e nada mais precisa mudar.
        servicos.AddSingleton<ISistemaDeAmortizacao, TabelaPrice>();
        servicos.AddSingleton<ISistemaDeAmortizacao, TabelaSac>();
        servicos.AddSingleton<SistemasDeAmortizacao>();

        // A ordem do registro e a ordem em que as regras aparecem no laudo. Impedimento
        // primeiro, condicao do produto depois, capacidade de pagamento por ultimo — e
        // como um analista leria.
        servicos.AddSingleton<IRegraDeCredito, RestricaoCadastral>();
        servicos.AddSingleton<IRegraDeCredito, ScoreMinimo>();
        servicos.AddSingleton<IRegraDeCredito, ValorDentroDoProduto>();
        servicos.AddSingleton<IRegraDeCredito, PrazoDentroDoProduto>();
        servicos.AddSingleton<IRegraDeCredito, ComprometimentoDeRenda>();
        servicos.AddSingleton<MotorDeDecisao>();

        servicos.AddScoped<IValidator<PedidoDeCadastro>, ValidadorDeCadastro>();

        servicos.AddProblemDetails();
        servicos.AddExceptionHandler<TratamentoDeErrosDeDominio>();

        return servicos.AdicionarLimiteDeSubmissao();
    }

    private static IServiceCollection AdicionarLimiteDeSubmissao(this IServiceCollection servicos) =>
        servicos.AddRateLimiter(limitador =>
        {
            limitador.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limitador.AddPolicy(EndpointsDePropostas.PoliticaDeLimite, contexto =>
            {
                // Lido por requisicao pelo mesmo motivo da string de conexao. O limitador
                // de cada particao e criado uma vez e guardado, entao mudar o valor em
                // tempo de execucao so vale para particao que ainda nao apareceu.
                var limite = contexto.RequestServices
                    .GetRequiredService<IOptionsMonitor<LimiteDeSubmissao>>().CurrentValue;

                return RateLimitPartition.GetFixedWindowLimiter(
                    Identificar(contexto),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limite.Permitidas,
                        Window = TimeSpan.FromSeconds(limite.JanelaEmSegundos),

                        // Fila zero: quem passou do limite recebe 429 na hora. Enfileirar
                        // so empurraria a espera para o cliente sem aliviar o servidor.
                        QueueLimit = 0,
                    });
            });

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

    /// <remarks>
    /// Particiona por IP de conexao. Atras de proxy ou balanceador isso vira o IP do
    /// proxy e o limite passa a valer para todo mundo junto — nesse caso e preciso
    /// ligar ForwardedHeaders antes, com a lista de proxies confiaveis.
    /// </remarks>
    private static string Identificar(HttpContext contexto) =>
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
}
