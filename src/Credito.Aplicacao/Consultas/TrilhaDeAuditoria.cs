using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Consultas;

/// <summary>
/// Os tipos que o campo <see cref="EventoDaTrilha.Tipo"/> pode assumir.
/// </summary>
public static class TiposDeEvento
{
    public const string Estado = "estado";

    public const string Decisao = "decisao";

    public const string Regra = "regra";

    public const string Contrato = "contrato";

    public const string Pagamento = "pagamento";
}

/// <param name="Tipo">
/// Um dos valores de <see cref="TiposDeEvento"/>. Quem consome filtra por ele sem
/// precisar interpretar o texto do resumo.
/// </param>
/// <param name="Origem">Quem causou o evento, ex: "api:submissao", "motor:decisao".</param>
public sealed record EventoDaTrilha(
    DateTimeOffset OcorridoEm,
    string Tipo,
    string Resumo,
    string Origem);

/// <summary>
/// A vida da proposta em uma linha do tempo so.
/// </summary>
/// <remarks>
/// Derivada, e nao gravada: cada evento ja existe em algum lugar — a transicao, o laudo, a
/// parcela paga. Uma segunda copia em tabela propria seria uma segunda versao da verdade, e
/// a que divergisse seria justamente a que ninguem le no dia a dia.
/// </remarks>
/// <param name="Cpf">Mascarado, como em toda leitura.</param>
public sealed record TrilhaDeAuditoria(
    Guid PropostaId,
    EstadoDaProposta Estado,
    string NomeSolicitante,
    string Cpf,
    IReadOnlyList<EventoDaTrilha> Eventos);
