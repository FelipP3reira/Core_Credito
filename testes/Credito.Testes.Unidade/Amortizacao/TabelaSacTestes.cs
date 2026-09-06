using Credito.Dominio.Amortizacao;

namespace Credito.Testes.Unidade.Amortizacao;

public class TabelaSacTestes
{
    private static readonly TabelaSac Sac = new();

    [Fact]
    public void AmortizaSempreAMesmaFatiaEOsJurosCaemJunto()
    {
        var cronograma = Sac.Gerar(12_000m, 0.01m, 12);

        Assert.Equal(1_120m, cronograma.PrimeiraParcela);
        Assert.Equal(1_010m, cronograma.UltimaParcela);
        Assert.All(cronograma.Parcelas, parcela => Assert.Equal(1_000m, parcela.Amortizacao));
        Assert.Equal(12_780m, cronograma.TotalPago);
    }

    [Fact]
    public void ParcelaCaiTodoMes()
    {
        var parcelas = Sac.Gerar(50_000m, 0.02m, 36).Parcelas;

        for (var atual = 1; atual < parcelas.Count; atual++)
        {
            Assert.True(parcelas[atual].Valor < parcelas[atual - 1].Valor);
        }
    }

    [Fact]
    public void SemJurosTodasAsParcelasFicamIguais()
    {
        var cronograma = Sac.Gerar(1_200m, 0m, 12);

        Assert.All(cronograma.Parcelas, parcela => Assert.Equal(100m, parcela.Valor));
    }

    [Fact]
    public void FechaExatoQuandoADivisaoNaoEhRedonda()
    {
        var cronograma = Sac.Gerar(1_000m, 0.0189m, 7);

        Assert.Equal(1_000m, cronograma.Parcelas.Sum(parcela => parcela.Amortizacao));
        Assert.All(
            cronograma.Parcelas.SkipLast(1),
            parcela => Assert.Equal(142.86m, parcela.Amortizacao));

        // 142,86 vezes seis passa de 857,16, entao a ultima leva 142,84.
        Assert.Equal(142.84m, cronograma.Parcelas[^1].Amortizacao);
    }

    [Fact]
    public void GuardaAEntradaNoCronograma()
    {
        var cronograma = Sac.Gerar(12_000m, 0.01m, 12);

        Assert.Equal(SistemaDeAmortizacao.Sac, cronograma.Sistema);
    }
}
