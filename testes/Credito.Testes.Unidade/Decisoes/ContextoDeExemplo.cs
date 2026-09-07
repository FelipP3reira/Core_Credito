using Credito.Dominio.Amortizacao;
using Credito.Dominio.Decisoes;
using Credito.Dominio.Politicas;

namespace Credito.Testes.Unidade.Decisoes;

internal static class ContextoDeExemplo
{
    public static readonly DateTimeOffset Agora = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    public static readonly Guid PropostaId = Guid.CreateVersion7();

    /// <summary>Faixas cobrindo a escala inteira, da pior taxa para a melhor.</summary>
    public static IEnumerable<FaixaDeTaxa> Faixas() =>
    [
        new FaixaDeTaxa(0, 499, 0.049m),
        new FaixaDeTaxa(500, 699, 0.029m),
        new FaixaDeTaxa(700, 849, 0.019m),
        new FaixaDeTaxa(850, 1000, 0.012m),
    ];

    public static PoliticaDeCredito Politica(
        int versao = 1,
        int scoreMinimo = 500,
        decimal comprometimentoMaximo = 0.30m,
        decimal valorMinimo = 1_000m,
        decimal valorMaximo = 100_000m,
        int prazoMinimoEmMeses = 6,
        int prazoMaximoEmMeses = 96,
        int validadeDaAprovacaoEmDias = 30,
        IEnumerable<FaixaDeTaxa>? faixas = null) =>
        new(
            versao,
            scoreMinimo,
            comprometimentoMaximo,
            valorMinimo,
            valorMaximo,
            prazoMinimoEmMeses,
            prazoMaximoEmMeses,
            validadeDaAprovacaoEmDias,
            Agora,
            faixas ?? Faixas());

    public static ContextoDaAnalise Contexto(
        decimal valorSolicitado = 20_000m,
        int prazoEmMeses = 24,
        decimal rendaMensal = 8_500m,
        int score = 720,
        bool temRestricaoCadastral = false,
        decimal? taxaMensal = null,
        PoliticaDeCredito? politica = null,
        bool comCronograma = true)
    {
        var aplicada = politica ?? Politica();
        var taxa = taxaMensal ?? aplicada.TaxaPara(score);

        return new ContextoDaAnalise(
            PropostaId,
            valorSolicitado,
            prazoEmMeses,
            rendaMensal,
            score,
            temRestricaoCadastral,
            taxa,
            comCronograma ? new TabelaPrice().Gerar(valorSolicitado, taxa, prazoEmMeses) : null,
            aplicada);
    }
}
