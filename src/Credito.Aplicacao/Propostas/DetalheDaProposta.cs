using Credito.Dominio.Amortizacao;
using Credito.Aplicacao.Analises;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Propostas;

public sealed record LaudoDaDecisao(
    bool Aprovada,
    int ScoreObservado,
    decimal TaxaMensalAplicada,
    int VersaoDaPolitica,
    DateTimeOffset AvaliadaEm,
    IReadOnlyList<LinhaDoLaudo> Regras);

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
    IReadOnlyList<MudancaDeEstado> Historico,
    LaudoDaDecisao? Decisao);
