namespace Credito.Dominio.Amortizacao;

public sealed class Cronograma
{
    internal Cronograma(
        decimal valorFinanciado,
        decimal taxaMensal,
        SistemaDeAmortizacao sistema,
        IReadOnlyList<Parcela> parcelas)
    {
        ValorFinanciado = valorFinanciado;
        TaxaMensal = taxaMensal;
        Sistema = sistema;
        Parcelas = parcelas;
    }

    public decimal ValorFinanciado { get; }

    /// <summary>Taxa ao mes como fracao: 0,0189 e 1,89% a.m.</summary>
    public decimal TaxaMensal { get; }

    public SistemaDeAmortizacao Sistema { get; }

    public IReadOnlyList<Parcela> Parcelas { get; }

    public decimal TotalPago => Parcelas.Sum(parcela => parcela.Valor);

    public decimal TotalDeJuros => TotalPago - ValorFinanciado;

    public decimal PrimeiraParcela => Parcelas[0].Valor;

    public decimal UltimaParcela => Parcelas[^1].Valor;
}
