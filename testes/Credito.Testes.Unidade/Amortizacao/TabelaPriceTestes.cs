using Credito.Dominio.Amortizacao;

namespace Credito.Testes.Unidade.Amortizacao;

public class TabelaPriceTestes
{
    private static readonly TabelaPrice Price = new();

    /// <summary>
    /// Dez mil a 1% ao mes em doze meses da 888,49 — o exemplo de tabela Price que
    /// aparece em qualquer material de matematica financeira, e por isso serve de
    /// ancora contra a formula ter sido escrita ao contrario.
    /// </summary>
    [Fact]
    public void BateComOExemploClassicoDaTabela()
    {
        var cronograma = Price.Gerar(10_000m, 0.01m, 12);

        Assert.Equal(888.49m, cronograma.Parcelas[0].Valor);
        Assert.Equal(788.49m, cronograma.Parcelas[0].Amortizacao);
        Assert.Equal(100.00m, cronograma.Parcelas[0].Juros);
        Assert.Equal(10_000m, cronograma.Parcelas.Sum(parcela => parcela.Amortizacao));
    }

    [Fact]
    public void TodasAsParcelasSaoIguaisMenosAUltima()
    {
        var cronograma = Price.Gerar(10_000m, 0.01m, 12);

        Assert.All(
            cronograma.Parcelas.SkipLast(1),
            parcela => Assert.Equal(888.49m, parcela.Valor));
    }

    /// <summary>
    /// A ultima parcela absorve a sobra do arredondamento das anteriores. Se ela fosse
    /// calculada como as outras, a soma das amortizacoes nao fecharia com o financiado
    /// e o contrato terminaria com centavo devendo ou sobrando.
    /// </summary>
    [Fact]
    public void AUltimaParcelaCarregaASobraDoArredondamento()
    {
        var cronograma = Price.Gerar(10_000m, 0.01m, 12);

        Assert.Equal(888.47m, cronograma.UltimaParcela);
        Assert.NotEqual(cronograma.PrimeiraParcela, cronograma.UltimaParcela);
        Assert.True(Math.Abs(cronograma.PrimeiraParcela - cronograma.UltimaParcela) < 1m);
    }

    [Fact]
    public void AmortizacaoCresceEJurosCaemAoLongoDoContrato()
    {
        var parcelas = Price.Gerar(50_000m, 0.02m, 36).Parcelas;

        for (var atual = 1; atual < parcelas.Count; atual++)
        {
            Assert.True(parcelas[atual].Amortizacao > parcelas[atual - 1].Amortizacao);
            Assert.True(parcelas[atual].Juros < parcelas[atual - 1].Juros);
        }
    }

    [Fact]
    public void SemJurosAParcelaEhOFinanciadoDivididoPeloPrazo()
    {
        var cronograma = Price.Gerar(1_200m, 0m, 12);

        Assert.All(cronograma.Parcelas, parcela =>
        {
            Assert.Equal(100m, parcela.Valor);
            Assert.Equal(0m, parcela.Juros);
        });
    }

    // Divisao que nao fecha: 1000 em 7 meses a 1,89% obriga a sobra a ir para o fim.
    [Fact]
    public void FechaExatoQuandoADivisaoNaoEhRedonda()
    {
        var cronograma = Price.Gerar(1_000m, 0.0189m, 7);

        Assert.Equal(153.86m, cronograma.PrimeiraParcela);
        Assert.Equal(153.84m, cronograma.UltimaParcela);
        Assert.Equal(1_000m, cronograma.Parcelas.Sum(parcela => parcela.Amortizacao));
    }

    [Fact]
    public void GuardaAEntradaNoCronograma()
    {
        var cronograma = Price.Gerar(10_000m, 0.01m, 12);

        Assert.Equal(10_000m, cronograma.ValorFinanciado);
        Assert.Equal(0.01m, cronograma.TaxaMensal);
        Assert.Equal(SistemaDeAmortizacao.Price, cronograma.Sistema);
    }
}
