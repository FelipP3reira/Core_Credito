namespace Credito.Dominio.Amortizacao;

/// <summary>
/// Parcela constante, amortizacao crescente. A parcela paga sempre os juros do mes e o
/// que sobra abate o saldo — como o saldo cai, sobra mais a cada mes.
/// </summary>
public sealed class TabelaPrice : ISistemaDeAmortizacao
{
    public SistemaDeAmortizacao Sistema => SistemaDeAmortizacao.Price;

    public Cronograma Gerar(decimal valorFinanciado, decimal taxaMensal, int prazoEmMeses)
    {
        RegrasDoCronograma.GarantirEntrada(valorFinanciado, taxaMensal, prazoEmMeses);

        var parcelaFixa = ValorDaParcela(valorFinanciado, taxaMensal, prazoEmMeses);
        var parcelas = new List<Parcela>(prazoEmMeses);
        var saldo = valorFinanciado;

        for (var numero = 1; numero <= prazoEmMeses; numero++)
        {
            var juros = ContaDeDinheiro.EmCentavos(saldo * taxaMensal);

            // Na ultima parcela a amortizacao e o que sobrou, e nao o resultado da conta.
            // E o que faz a soma das amortizacoes fechar exatamente com o valor financiado
            // mesmo depois de arredondar cada parcela a centavo: a sobra do arredondamento
            // vai toda para o fim, que e como banco faz.
            var amortizacao = numero == prazoEmMeses
                ? saldo
                : RegrasDoCronograma.GarantirQueAmortiza(
                    ContaDeDinheiro.EmCentavos(parcelaFixa - juros), saldo);

            saldo -= amortizacao;
            parcelas.Add(new Parcela(numero, amortizacao, juros, amortizacao + juros, saldo));
        }

        return new Cronograma(valorFinanciado, taxaMensal, Sistema, parcelas);
    }

    /// <summary>
    /// PMT = PV.i.(1+i)^n dividido por ((1+i)^n - 1).
    /// </summary>
    /// <remarks>
    /// Forma equivalente a PV.i / (1 - (1+i) elevado a -n), escolhida para o expoente
    /// ficar positivo e a potencia poder ser feita por multiplicacao em decimal.
    /// </remarks>
    private static decimal ValorDaParcela(decimal valorFinanciado, decimal taxaMensal, int prazoEmMeses)
    {
        // Sem juros nao ha o que a formula resolva, e ela dividiria por zero.
        if (taxaMensal == 0)
        {
            return ContaDeDinheiro.EmCentavos(valorFinanciado / prazoEmMeses);
        }

        var fator = ContaDeDinheiro.Potencia(1 + taxaMensal, prazoEmMeses);

        return ContaDeDinheiro.EmCentavos(valorFinanciado * taxaMensal * fator / (fator - 1));
    }
}
