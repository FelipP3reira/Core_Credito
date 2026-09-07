using Credito.Dominio.Amortizacao;

namespace Credito.Aplicacao.Propostas;

public sealed record ParcelaSimulada(
    int Numero,
    decimal Amortizacao,
    decimal Juros,
    decimal Valor,
    decimal SaldoDevedor);

/// <param name="ScoreObservado">
/// Vai na resposta porque e o que explica a taxa: sem ele, quem simula ve o numero e nao
/// tem como saber de onde veio.
/// </param>
public sealed record SimulacaoDaProposta(
    Guid PropostaId,
    decimal ValorFinanciado,
    decimal TaxaMensal,
    int PrazoEmMeses,
    SistemaDeAmortizacao Sistema,
    int ScoreObservado,
    decimal PrimeiraParcela,
    decimal UltimaParcela,
    decimal TotalPago,
    decimal TotalDeJuros,
    IReadOnlyList<ParcelaSimulada> Parcelas);
