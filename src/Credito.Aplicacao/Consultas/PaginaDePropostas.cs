namespace Credito.Aplicacao.Consultas;

/// <param name="Proximo">
/// Marcador para pedir a pagina seguinte, ou nulo quando esta foi a ultima.
/// </param>
public sealed record PaginaDePropostas(IReadOnlyList<LinhaDaProposta> Itens, string? Proximo);
