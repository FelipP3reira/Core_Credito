using Credito.Dominio.Erros;

namespace Credito.Dominio.Amortizacao;

internal static class ContaDeDinheiro
{
    /// <summary>
    /// Arredondamento comercial: 0,005 sobe para 0,01.
    /// </summary>
    /// <remarks>
    /// O padrao do .NET e o bancario, que leva a metade exata para o par mais proximo.
    /// Em parcela de emprestimo isso desvia do que o cliente confere no boleto e do que
    /// a planilha do escritorio calcula.
    /// </remarks>
    public static decimal EmCentavos(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Potencia de expoente inteiro sem sair do decimal.
    /// </summary>
    /// <remarks>
    /// Math.Pow so existe em double, e levar dinheiro para ponto flutuante binario e
    /// trazer de volta e a origem classica do centavo que falta no fim do contrato.
    /// Como o expoente aqui e o prazo em meses, sempre inteiro, multiplicar repetido
    /// resolve e mantem a precisao do decimal.
    /// </remarks>
    public static decimal Potencia(decimal baseDaConta, int expoente)
    {
        var resultado = 1m;

        try
        {
            for (var vez = 0; vez < expoente; vez++)
            {
                resultado *= baseDaConta;
            }
        }
        catch (OverflowException causa)
        {
            throw new AmortizacaoInvalidaException(
                "Taxa e prazo combinados estouram a faixa do calculo.", causa);
        }

        return resultado;
    }
}
