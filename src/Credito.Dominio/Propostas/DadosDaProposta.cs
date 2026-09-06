using Credito.Dominio.Amortizacao;
using Credito.Dominio.Comum;

namespace Credito.Dominio.Propostas;

/// <summary>Dados brutos de entrada de uma proposta, ainda nao validados como agregado.</summary>
public sealed record DadosDaProposta(
    string ChaveIdempotencia,
    string ImpressaoDoPedido,
    CpfProtegido Cpf,
    string NomeSolicitante,
    DateOnly DataDeNascimento,
    decimal RendaMensal,
    decimal ValorSolicitado,
    int PrazoEmMeses,
    SistemaDeAmortizacao Sistema);
