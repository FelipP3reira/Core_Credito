using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Analises;

public sealed record LinhaDoLaudo(
    string Codigo,
    bool Aprovou,
    string Motivo,
    decimal? ValorObservado,
    decimal? LimiteExigido);

public sealed record ResultadoDaDecisao(
    Guid PropostaId,
    EstadoDaProposta Estado,
    bool Aprovada,
    int ScoreObservado,
    decimal TaxaMensalAplicada,
    int VersaoDaPolitica,
    DateTimeOffset AvaliadaEm,
    IReadOnlyList<LinhaDoLaudo> Laudo);
