namespace Credito.Dominio.Amortizacao;

public interface ISistemaDeAmortizacao
{
    SistemaDeAmortizacao Sistema { get; }

    /// <param name="taxaMensal">Fracao ao mes: 0,0189 e 1,89% a.m.</param>
    Cronograma Gerar(decimal valorFinanciado, decimal taxaMensal, int prazoEmMeses);
}
