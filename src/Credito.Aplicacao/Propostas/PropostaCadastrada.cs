using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Propostas;

/// <param name="JaExistia">
/// Verdadeiro quando o pedido foi reenvio de uma proposta ja gravada. A API traduz
/// em 200 no lugar de 201, para o cliente distinguir sem precisar comparar o corpo.
/// </param>
public sealed record PropostaCadastrada(Guid Id, EstadoDaProposta Estado, bool JaExistia);
