namespace Credito.Dominio.Propostas;

public enum SistemaDeAmortizacao
{
    /// <summary>Parcela constante, amortizacao crescente.</summary>
    Price = 1,

    /// <summary>Amortizacao constante, parcela decrescente.</summary>
    Sac = 2,
}
