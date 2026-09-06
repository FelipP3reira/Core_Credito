namespace Credito.Dominio.Amortizacao;

/// <summary>
/// Amortizacao constante, parcela decrescente. Abate sempre a mesma fatia do saldo, e
/// como o saldo cai, os juros do mes caem junto — a primeira parcela e a mais cara.
/// </summary>
public sealed class TabelaSac : ISistemaDeAmortizacao
{
    public SistemaDeAmortizacao Sistema => SistemaDeAmortizacao.Sac;

    public Cronograma Gerar(decimal valorFinanciado, decimal taxaMensal, int prazoEmMeses)
    {
        RegrasDoCronograma.GarantirEntrada(valorFinanciado, taxaMensal, prazoEmMeses);

        var amortizacaoConstante = ContaDeDinheiro.EmCentavos(valorFinanciado / prazoEmMeses);
        var parcelas = new List<Parcela>(prazoEmMeses);
        var saldo = valorFinanciado;

        for (var numero = 1; numero <= prazoEmMeses; numero++)
        {
            var juros = ContaDeDinheiro.EmCentavos(saldo * taxaMensal);

            // Mesma ideia da Price: a ultima parcela leva a sobra do arredondamento.
            var amortizacao = numero == prazoEmMeses
                ? saldo
                : RegrasDoCronograma.GarantirQueAmortiza(amortizacaoConstante, saldo);

            saldo -= amortizacao;
            parcelas.Add(new Parcela(numero, amortizacao, juros, amortizacao + juros, saldo));
        }

        return new Cronograma(valorFinanciado, taxaMensal, Sistema, parcelas);
    }
}
