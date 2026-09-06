using Credito.Dominio.Amortizacao;

namespace Credito.Aplicacao.Propostas;

public sealed record ParcelaSimulada(
    int Numero,
    decimal Amortizacao,
    decimal Juros,
    decimal Valor,
    decimal SaldoDevedor);

public sealed record SimulacaoDaProposta(
    Guid PropostaId,
    decimal ValorFinanciado,
    decimal TaxaMensal,
    int PrazoEmMeses,
    SistemaDeAmortizacao Sistema,
    decimal PrimeiraParcela,
    decimal UltimaParcela,
    decimal TotalPago,
    decimal TotalDeJuros,
    IReadOnlyList<ParcelaSimulada> Parcelas);
