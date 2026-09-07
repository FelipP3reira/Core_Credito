using Credito.Dominio.Erros;
using Credito.Dominio.Politicas;

namespace Credito.Testes.Unidade.Decisoes;

public class PoliticaDeCreditoTestes
{
    [Theory]
    [InlineData(0, 0.049)]
    [InlineData(499, 0.049)]
    [InlineData(500, 0.029)]
    [InlineData(699, 0.029)]
    [InlineData(700, 0.019)]
    [InlineData(849, 0.019)]
    [InlineData(850, 0.012)]
    [InlineData(1000, 0.012)]
    public void DevolveATaxaDaFaixaQueCobreOScore(int score, decimal esperada) =>
        Assert.Equal(esperada, ContextoDeExemplo.Politica().TaxaPara(score));

    /// <summary>
    /// Score abaixo do minimo ainda recebe taxa — a da pior faixa. E o que permite montar
    /// o cronograma e avaliar TODAS as regras: a proposta e negada pela regra de score, e
    /// nao por faltar taxa no meio da analise.
    /// </summary>
    [Fact]
    public void ScoreAbaixoDoMinimoAindaTemTaxa()
    {
        var politica = ContextoDeExemplo.Politica(scoreMinimo: 500);

        Assert.Equal(0.049m, politica.TaxaPara(120));
    }

    [Fact]
    public void BuracoEntreFaixasEhRecusado()
    {
        var comBuraco = new[]
        {
            new FaixaDeTaxa(0, 499, 0.049m),
            new FaixaDeTaxa(600, 1000, 0.019m),
        };

        var erro = Assert.Throws<PoliticaInvalidaException>(
            () => ContextoDeExemplo.Politica(faixas: comBuraco));

        Assert.Contains("contiguas", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SobreposicaoEntreFaixasEhRecusada()
    {
        var sobrepostas = new[]
        {
            new FaixaDeTaxa(0, 600, 0.049m),
            new FaixaDeTaxa(500, 1000, 0.019m),
        };

        Assert.Throws<PoliticaInvalidaException>(() => ContextoDeExemplo.Politica(faixas: sobrepostas));
    }

    [Fact]
    public void FaixasQueNaoChegamAoTopoDaEscalaSaoRecusadas()
    {
        var curtas = new[] { new FaixaDeTaxa(0, 900, 0.049m) };

        var erro = Assert.Throws<PoliticaInvalidaException>(
            () => ContextoDeExemplo.Politica(faixas: curtas));

        Assert.Contains("0 a 1000", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FaixasQueNaoComecamNoZeroSaoRecusadas()
    {
        var altas = new[] { new FaixaDeTaxa(1, 1000, 0.049m) };

        Assert.Throws<PoliticaInvalidaException>(() => ContextoDeExemplo.Politica(faixas: altas));
    }

    [Fact]
    public void PoliticaSemFaixaEhRecusada() =>
        Assert.Throws<PoliticaInvalidaException>(() => ContextoDeExemplo.Politica(faixas: []));

    [Fact]
    public void OrdemDeEntradaDasFaixasNaoImporta()
    {
        var embaralhadas = new[]
        {
            new FaixaDeTaxa(850, 1000, 0.012m),
            new FaixaDeTaxa(0, 499, 0.049m),
            new FaixaDeTaxa(700, 849, 0.019m),
            new FaixaDeTaxa(500, 699, 0.029m),
        };

        Assert.Equal(0.029m, ContextoDeExemplo.Politica(faixas: embaralhadas).TaxaPara(600));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void VersaoPrecisaComecarEmUm(int versao) =>
        Assert.Throws<PoliticaInvalidaException>(() => ContextoDeExemplo.Politica(versao: versao));

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    public void ScoreMinimoForaDaEscalaEhRecusado(int scoreMinimo) =>
        Assert.Throws<PoliticaInvalidaException>(() => ContextoDeExemplo.Politica(scoreMinimo: scoreMinimo));

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public void ComprometimentoPrecisaSerFracaoEntreZeroEUm(decimal comprometimento) =>
        Assert.Throws<PoliticaInvalidaException>(
            () => ContextoDeExemplo.Politica(comprometimentoMaximo: comprometimento));

    [Fact]
    public void FaixaDeValorInvertidaEhRecusada() =>
        Assert.Throws<PoliticaInvalidaException>(
            () => ContextoDeExemplo.Politica(valorMinimo: 50_000m, valorMaximo: 1_000m));

    [Fact]
    public void FaixaDePrazoInvertidaEhRecusada() =>
        Assert.Throws<PoliticaInvalidaException>(
            () => ContextoDeExemplo.Politica(prazoMinimoEmMeses: 96, prazoMaximoEmMeses: 6));
}
