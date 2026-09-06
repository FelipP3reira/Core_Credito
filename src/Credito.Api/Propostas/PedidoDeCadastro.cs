using Credito.Dominio.Propostas;

namespace Credito.Api.Propostas;

public sealed record PedidoDeCadastro(
    string Cpf,
    string NomeSolicitante,
    DateOnly DataDeNascimento,
    decimal RendaMensal,
    decimal ValorSolicitado,
    int PrazoEmMeses,
    SistemaDeAmortizacao Sistema);
