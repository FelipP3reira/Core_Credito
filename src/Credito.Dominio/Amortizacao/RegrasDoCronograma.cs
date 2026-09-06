using Credito.Dominio.Erros;

namespace Credito.Dominio.Amortizacao;

/// <summary>
/// O que precisa valer para um cronograma existir, conferido antes e durante a geracao.
/// </summary>
internal static class RegrasDoCronograma
{
    public const int PrazoMaximoEmMeses = 480;

    public static void GarantirEntrada(decimal valorFinanciado, decimal taxaMensal, int prazoEmMeses)
    {
        if (valorFinanciado <= 0)
        {
            throw new AmortizacaoInvalidaException("Valor financiado precisa ser maior que zero.");
        }

        if (taxaMensal < 0)
        {
            throw new AmortizacaoInvalidaException("Taxa mensal nao pode ser negativa.");
        }

        if (prazoEmMeses < 1 || prazoEmMeses > PrazoMaximoEmMeses)
        {
            throw new AmortizacaoInvalidaException(
                $"Prazo precisa ficar entre 1 e {PrazoMaximoEmMeses} meses.");
        }
    }

    /// <summary>
    /// Barra a parcela que nao amortiza nada e a que passaria do saldo devedor.
    /// </summary>
    /// <remarks>
    /// Conferir aqui, e nao por formula na entrada, e uma escolha. A formula teria de
    /// prever quanto o arredondamento a centavo acumula ao longo do contrato, e isso
    /// depende do valor, da taxa e do prazo ao mesmo tempo. Um exemplo de como erra
    /// facil: R$ 1.292,40 a 1,89% ao mes em 360 meses tem parcela de R$ 24,46 contra
    /// R$ 24,43 de juros no primeiro mes — a amortizacao teorica e de tres centavos, mas
    /// o meio centavo em que a propria parcela foi arredondada se acumula e a divida
    /// zera na parcela 352.
    /// <para>
    /// Recusar e melhor que entregar cronograma curto ou com parcela zerada no fim: nesse
    /// caso a combinacao de valor, taxa e prazo simplesmente nao se representa em centavos.
    /// </para>
    /// </remarks>
    public static decimal GarantirQueAmortiza(decimal amortizacao, decimal saldo)
    {
        // Parcela que nao abate nem um centavo transforma o contrato em pagamento de juros
        // com o principal inteiro no fim — o que e um balao, e nao o sistema pedido.
        if (amortizacao <= 0)
        {
            throw new AmortizacaoInvalidaException(
                "Valor, taxa e prazo nao fecham em centavos: a parcela nao cobre nem um centavo de amortizacao.");
        }

        if (amortizacao > saldo)
        {
            throw new AmortizacaoInvalidaException(
                "Valor, taxa e prazo nao fecham em centavos: o saldo zeraria antes da ultima parcela.");
        }

        return amortizacao;
    }
}
