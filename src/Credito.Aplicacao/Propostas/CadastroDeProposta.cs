using Credito.Dominio.Amortizacao;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Propostas;

public sealed record CadastroDeProposta(
    string ChaveIdempotencia,
    string Cpf,
    string NomeSolicitante,
    DateOnly DataDeNascimento,
    decimal RendaMensal,
    decimal ValorSolicitado,
    int PrazoEmMeses,
    SistemaDeAmortizacao Sistema);
