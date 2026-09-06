using Credito.Dominio.Amortizacao;
using Credito.Dominio.Erros;

namespace Credito.Testes.Unidade.Amortizacao;

/// <summary>
/// O que precisa valer nos dois sistemas, varrido sobre uma faixa larga de entradas.
/// </summary>
/// <remarks>
/// Varredura em vez de casos escolhidos a dedo porque o risco aqui e de arredondamento:
/// o erro nao aparece num caso bonito como dez mil em doze meses, aparece quando a divisao
/// nao fecha e a sobra de centavo se acumula por centenas de parcelas. Foi assim que
/// apareceu o caso de sete reais em 360 meses, que quebrava o saldo devedor.
/// </remarks>
public class InvariantesDaAmortizacaoTestes
{
    private static readonly SistemasDeAmortizacao Sistemas = new([new TabelaPrice(), new TabelaSac()]);

    private static readonly decimal[] Valores = [7m, 1_000m, 12_345.67m, 250_000m, 999_999.99m];
    private static readonly decimal[] Taxas = [0m, 0.0001m, 0.0189m, 0.05m, 0.10m];
    private static readonly int[] Prazos = [1, 2, 3, 7, 12, 24, 360, 480];

    /// <summary>
    /// Só entram as combinações que produzem cronograma. As que não fecham em centavos
    /// têm teste próprio abaixo, e não somem daqui em silêncio: se a geração passasse a
    /// falhar para tudo, esta lista ficaria vazia e o xUnit reprova Theory sem dados.
    /// </summary>
    public static TheoryData<SistemaDeAmortizacao, decimal, decimal, int> Combinacoes()
    {
        var casos = new TheoryData<SistemaDeAmortizacao, decimal, decimal, int>();

        foreach (var sistema in Enum.GetValues<SistemaDeAmortizacao>())
        {
            foreach (var valor in Valores)
            {
                foreach (var taxa in Taxas)
                {
                    foreach (var prazo in Prazos.Where(prazo => Fecha(sistema, valor, taxa, prazo)))
                    {
                        casos.Add(sistema, valor, taxa, prazo);
                    }
                }
            }
        }

        return casos;
    }

    private static bool Fecha(SistemaDeAmortizacao sistema, decimal valor, decimal taxa, int prazo)
    {
        try
        {
            Gerar(sistema, valor, taxa, prazo);
            return true;
        }
        catch (AmortizacaoInvalidaException)
        {
            return false;
        }
    }

    private static Cronograma Gerar(SistemaDeAmortizacao sistema, decimal valor, decimal taxa, int prazo) =>
        Sistemas.De(sistema).Gerar(valor, taxa, prazo);

    [Theory]
    [MemberData(nameof(Combinacoes))]
    public void SomaDasAmortizacoesFechaExatamenteComOValorFinanciado(
        SistemaDeAmortizacao sistema, decimal valor, decimal taxa, int prazo)
    {
        var cronograma = Gerar(sistema, valor, taxa, prazo);

        Assert.Equal(valor, cronograma.Parcelas.Sum(parcela => parcela.Amortizacao));
    }

    [Theory]
    [MemberData(nameof(Combinacoes))]
    public void SaldoTerminaZeradoENuncaFicaNegativo(
        SistemaDeAmortizacao sistema, decimal valor, decimal taxa, int prazo)
    {
        var cronograma = Gerar(sistema, valor, taxa, prazo);

        Assert.All(cronograma.Parcelas, parcela => Assert.True(parcela.SaldoDevedor >= 0));
        Assert.Equal(0m, cronograma.Parcelas[^1].SaldoDevedor);
    }

    [Theory]
    [MemberData(nameof(Combinacoes))]
    public void CadaParcelaEhAmortizacaoMaisJuros(
        SistemaDeAmortizacao sistema, decimal valor, decimal taxa, int prazo)
    {
        var cronograma = Gerar(sistema, valor, taxa, prazo);

        Assert.All(cronograma.Parcelas, parcela =>
            Assert.Equal(parcela.Amortizacao + parcela.Juros, parcela.Valor));
    }

    [Theory]
    [MemberData(nameof(Combinacoes))]
    public void SaiUmaParcelaPorMesNumeradaEmSequencia(
        SistemaDeAmortizacao sistema, decimal valor, decimal taxa, int prazo)
    {
        var cronograma = Gerar(sistema, valor, taxa, prazo);

        Assert.Equal(prazo, cronograma.Parcelas.Count);
        Assert.Equal(
            Enumerable.Range(1, prazo),
            cronograma.Parcelas.Select(parcela => parcela.Numero));
    }

    [Theory]
    [MemberData(nameof(Combinacoes))]
    public void TudoTemNoMaximoDoisDigitosDepoisDaVirgula(
        SistemaDeAmortizacao sistema, decimal valor, decimal taxa, int prazo)
    {
        var cronograma = Gerar(sistema, valor, taxa, prazo);

        Assert.All(cronograma.Parcelas, parcela =>
        {
            Assert.Equal(parcela.Amortizacao, Math.Round(parcela.Amortizacao, 2));
            Assert.Equal(parcela.Juros, Math.Round(parcela.Juros, 2));
            Assert.Equal(parcela.Valor, Math.Round(parcela.Valor, 2));
        });
    }

    [Theory]
    [MemberData(nameof(Combinacoes))]
    public void TotalDeJurosEhOQueSePagaAlemDoFinanciado(
        SistemaDeAmortizacao sistema, decimal valor, decimal taxa, int prazo)
    {
        var cronograma = Gerar(sistema, valor, taxa, prazo);

        Assert.Equal(cronograma.TotalPago - valor, cronograma.TotalDeJuros);
        Assert.Equal(cronograma.Parcelas.Sum(parcela => parcela.Juros), cronograma.TotalDeJuros);
    }

    [Theory]
    [MemberData(nameof(Combinacoes))]
    public void SemJurosNaoSePagaNadaAlemDoFinanciado(
        SistemaDeAmortizacao sistema, decimal valor, decimal taxa, int prazo)
    {
        if (taxa != 0)
        {
            return;
        }

        Assert.Equal(valor, Gerar(sistema, valor, taxa, prazo).TotalPago);
    }

    [Theory]
    [InlineData(SistemaDeAmortizacao.Price)]
    [InlineData(SistemaDeAmortizacao.Sac)]
    public void ParcelaUnicaQuitaTudoComOsJurosDeUmMes(SistemaDeAmortizacao sistema)
    {
        var cronograma = Gerar(sistema, 5_000m, 0.02m, prazo: 1);

        var parcela = Assert.Single(cronograma.Parcelas);
        Assert.Equal(5_000m, parcela.Amortizacao);
        Assert.Equal(100m, parcela.Juros);
        Assert.Equal(5_100m, parcela.Valor);
    }

    [Theory]
    [InlineData(SistemaDeAmortizacao.Price)]
    [InlineData(SistemaDeAmortizacao.Sac)]
    public void RecusaValorNaoPositivo(SistemaDeAmortizacao sistema)
    {
        Assert.Throws<AmortizacaoInvalidaException>(() => Gerar(sistema, 0m, 0.01m, 12));
        Assert.Throws<AmortizacaoInvalidaException>(() => Gerar(sistema, -1m, 0.01m, 12));
    }

    [Theory]
    [InlineData(SistemaDeAmortizacao.Price)]
    [InlineData(SistemaDeAmortizacao.Sac)]
    public void RecusaTaxaNegativa(SistemaDeAmortizacao sistema) =>
        Assert.Throws<AmortizacaoInvalidaException>(() => Gerar(sistema, 10_000m, -0.01m, 12));

    [Theory]
    [InlineData(SistemaDeAmortizacao.Price, 0)]
    [InlineData(SistemaDeAmortizacao.Price, -3)]
    [InlineData(SistemaDeAmortizacao.Price, 481)]
    [InlineData(SistemaDeAmortizacao.Sac, 0)]
    [InlineData(SistemaDeAmortizacao.Sac, -3)]
    [InlineData(SistemaDeAmortizacao.Sac, 481)]
    public void RecusaPrazoForaDaFaixa(SistemaDeAmortizacao sistema, int prazo) =>
        Assert.Throws<AmortizacaoInvalidaException>(() => Gerar(sistema, 10_000m, 0.01m, prazo));

    /// <summary>
    /// Sete reais em 360 meses: 7/360 da 0,0194, que arredonda para 0,02, e 359 parcelas
    /// de dois centavos passam dos sete reais.
    /// </summary>
    [Theory]
    [InlineData(SistemaDeAmortizacao.Price)]
    [InlineData(SistemaDeAmortizacao.Sac)]
    public void RecusaValorPequenoDemaisParaOPrazo(SistemaDeAmortizacao sistema)
    {
        var erro = Assert.Throws<AmortizacaoInvalidaException>(() => Gerar(sistema, 7m, 0.01m, 360));

        Assert.Contains("nao fecham em centavos", erro.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nao e valor pequeno, e parcela que mal cobre os juros: R$ 1.292,40 a 1,89% ao mes
    /// em 360 meses tem parcela de R$ 24,46 contra R$ 24,43 de juros no primeiro mes. O
    /// meio centavo em que a parcela foi arredondada se acumula e a divida zera na 352.
    /// Recusar e melhor que devolver um cronograma de oito parcelas fantasma.
    /// </summary>
    [Fact]
    public void RecusaCombinacaoEmQueAParcelaMalCobreOsJuros()
    {
        var erro = Assert.Throws<AmortizacaoInvalidaException>(
            () => Gerar(SistemaDeAmortizacao.Price, 1_292.40m, 0.0189m, 360));

        Assert.Contains("nao fecham em centavos", erro.Message, StringComparison.Ordinal);
    }

    // Nao existe emprestimo assim, mas o calculo nao pode devolver numero errado em
    // silencio quando a potencia estoura a faixa do decimal.
    [Fact]
    public void TaxaAbsurdaComPrazoLongoFalhaExplicitamente() =>
        Assert.Throws<AmortizacaoInvalidaException>(
            () => Gerar(SistemaDeAmortizacao.Price, 10_000m, 1m, 480));
}
