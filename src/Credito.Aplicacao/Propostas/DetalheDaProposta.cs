using Credito.Dominio.Amortizacao;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Propostas;

public sealed record MudancaDeEstado(EstadoDaProposta De, EstadoDaProposta Para, DateTimeOffset OcorridaEm, string Origem);

/// <param name="Cpf">Sempre mascarado. Nao existe caminho de leitura que devolva o numero inteiro.</param>
public sealed record DetalheDaProposta(
    Guid Id,
    EstadoDaProposta Estado,
    string NomeSolicitante,
    string Cpf,
    decimal ValorSolicitado,
    int PrazoEmMeses,
    SistemaDeAmortizacao Sistema,
    DateTimeOffset CriadaEm,
    DateTimeOffset AtualizadaEm,
    IReadOnlyList<MudancaDeEstado> Historico);
