using Credito.Dominio.Amortizacao;

namespace Credito.Aplicacao.Consultas;

/// <param name="Numero">Nulo quando o contrato ja esta quitado.</param>
public sealed record ProximaParcela(int? Numero, DateOnly? Vencimento, decimal? Valor);

/// <summary>
/// Um contrato visto do lado da conta bancaria: o que a pessoa precisa saber para decidir
/// se paga hoje.
/// </summary>
/// <remarks>
/// Nao e o cronograma inteiro. Quem abre a area de emprestimo quer ver quanto ainda deve e
/// quando vence a proxima; as noventa e seis parcelas so interessam a quem abriu um
/// contrato especifico, e ai existe a rota do detalhe.
/// </remarks>
public sealed record ContratoDaConta(
    Guid ContratoId,
    Guid PropostaId,
    decimal ValorFinanciado,
    decimal TaxaMensal,
    SistemaDeAmortizacao Sistema,
    int PrazoEmMeses,
    decimal TotalPago,
    decimal SaldoAberto,
    int ParcelasPagas,
    bool EstaQuitado,
    DateTimeOffset AssinadoEm,
    DateTimeOffset? DesembolsadoEm,
    ProximaParcela Proxima);
