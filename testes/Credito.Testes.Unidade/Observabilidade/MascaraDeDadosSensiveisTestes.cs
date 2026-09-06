using System.Globalization;
using Credito.Infraestrutura.Observabilidade;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace Credito.Testes.Unidade.Observabilidade;

public class MascaraDeDadosSensiveisTestes
{
    private const string CpfEmTextoClaro = "52998224725";

    private sealed class ColetorDeEventos : ILogEventSink
    {
        public List<LogEvent> Eventos { get; } = [];

        public void Emit(LogEvent logEvent) => Eventos.Add(logEvent);
    }

    private sealed record Solicitante(string Nome, string Cpf, decimal RendaMensal);

    /// <summary>Registra o evento e devolve o que sairia gravado, ja formatado.</summary>
    private static string Registrar(Action<ILogger> escrita)
    {
        var coletor = new ColetorDeEventos();

        using (var registrador = new LoggerConfiguration()
            .Enrich.With(new MascaraDeDadosSensiveis())
            .WriteTo.Sink(coletor)
            .CreateLogger())
        {
            escrita(registrador);
        }

        var texto = new StringWriter(CultureInfo.InvariantCulture);
        foreach (var evento in coletor.Eventos)
        {
            new JsonFormatter().Format(evento, texto);
        }

        return texto.ToString();
    }

    [Fact]
    public void EscondeCampoSensivelPassadoSolto()
    {
        var saida = Registrar(registrador =>
            registrador.Information("cadastro recebido {Cpf} {RendaMensal}", CpfEmTextoClaro, 8_500m));

        Assert.DoesNotContain(CpfEmTextoClaro, saida, StringComparison.Ordinal);
        Assert.DoesNotContain("8500", saida, StringComparison.Ordinal);
    }

    [Fact]
    public void EscondeCampoSensivelDentroDeObjetoDestruturado()
    {
        var saida = Registrar(registrador => registrador.Information(
            "proposta {@Solicitante}",
            new Solicitante("Ana Ribeiro", CpfEmTextoClaro, 8_500m)));

        Assert.DoesNotContain(CpfEmTextoClaro, saida, StringComparison.Ordinal);
        Assert.DoesNotContain("8500", saida, StringComparison.Ordinal);
        Assert.Contains("Ana Ribeiro", saida, StringComparison.Ordinal);
    }

    [Fact]
    public void EscondeCampoSensivelDentroDeLista()
    {
        var saida = Registrar(registrador => registrador.Information(
            "lote {@Solicitantes}",
            new[] { new Solicitante("Ana Ribeiro", CpfEmTextoClaro, 8_500m) }));

        Assert.DoesNotContain(CpfEmTextoClaro, saida, StringComparison.Ordinal);
    }

    [Fact]
    public void EscondeValorDeChaveSensivelEmDicionario()
    {
        var saida = Registrar(registrador => registrador.Information(
            "formulario {@Campos}",
            new Dictionary<string, string> { ["cpf"] = CpfEmTextoClaro, ["cidade"] = "Belo Horizonte" }));

        Assert.DoesNotContain(CpfEmTextoClaro, saida, StringComparison.Ordinal);
        Assert.Contains("Belo Horizonte", saida, StringComparison.Ordinal);
    }

    // Mascarar demais tambem e defeito: log que esconde tudo nao serve para diagnostico.
    [Fact]
    public void PreservaCampoSemNadaDeSensivel()
    {
        var saida = Registrar(registrador =>
            registrador.Information("proposta {PropostaId} em {Estado}", "abc-123", "EmAnalise"));

        Assert.Contains("abc-123", saida, StringComparison.Ordinal);
        Assert.Contains("EmAnalise", saida, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Cpf")]
    [InlineData("CpfDoSolicitante")]
    [InlineData("cpf_solicitante")]
    [InlineData("RendaMensal")]
    [InlineData("SalarioBruto")]
    [InlineData("Senha")]
    [InlineData("Authorization")]
    [InlineData("ChaveDeCifra")]
    public void ReconheceAsVariacoesDeNomeQueAparecemNaPratica(string nomeDaPropriedade)
    {
        var saida = Registrar(registrador =>
            registrador.Information("diagnostico {" + nomeDaPropriedade + "}", "valor-secreto"));

        Assert.DoesNotContain("valor-secreto", saida, StringComparison.Ordinal);
    }
}
