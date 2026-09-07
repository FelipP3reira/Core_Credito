using Credito.Dominio.Propostas;

namespace Credito.Api.Propostas;

/// <param name="Cpf">
/// Obrigatorio. Vai no corpo, e nao na URL — e o motivo de esta busca ser um POST.
/// </param>
public sealed record PedidoDeBusca(
    string Cpf,
    EstadoDaProposta? Estado = null,
    DateTimeOffset? De = null,
    DateTimeOffset? Ate = null,
    string? Cursor = null,
    int? Tamanho = null);
