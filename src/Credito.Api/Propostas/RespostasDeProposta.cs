using Credito.Dominio.Propostas;

namespace Credito.Api.Propostas;

public sealed record RespostaDeCadastro(Guid Id, EstadoDaProposta Estado);

public sealed record RespostaDeAnalise(Guid Id, EstadoDaProposta Estado);
