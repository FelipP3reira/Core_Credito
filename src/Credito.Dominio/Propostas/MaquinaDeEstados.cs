using System.Collections.Frozen;
using Credito.Dominio.Erros;

namespace Credito.Dominio.Propostas;

/// <summary>
/// Transicoes validas da proposta. A ausencia de uma aresta e proibicao: o que nao
/// esta listado aqui nao acontece.
/// </summary>
/// <remarks>
/// <code>
/// Rascunho ──▶ EmAnalise ──▶ Aprovada ──▶ Contratada ──▶ Liquidada
///     │            │             │
///     │            ├──▶ Negada   └──▶ Expirada
///     └────────────┴──▶ Cancelada
/// </code>
/// Negada e terminal de proposito: reanalisar exige proposta nova, senao o laudo
/// da decisao anterior seria sobrescrito e a trilha de auditoria perderia o sentido.
/// Aprovada expira porque taxa aprovada tem validade — proposta nao fica de pe
/// para sempre esperando contratacao.
/// </remarks>
public static class MaquinaDeEstados
{
    private static readonly FrozenDictionary<EstadoDaProposta, EstadoDaProposta[]> Transicoes =
        new Dictionary<EstadoDaProposta, EstadoDaProposta[]>
        {
            [EstadoDaProposta.Rascunho] = [EstadoDaProposta.EmAnalise, EstadoDaProposta.Cancelada],
            [EstadoDaProposta.EmAnalise] = [EstadoDaProposta.Aprovada, EstadoDaProposta.Negada, EstadoDaProposta.Cancelada],
            [EstadoDaProposta.Aprovada] = [EstadoDaProposta.Contratada, EstadoDaProposta.Expirada],
            [EstadoDaProposta.Negada] = [],
            [EstadoDaProposta.Contratada] = [EstadoDaProposta.Liquidada],
            [EstadoDaProposta.Liquidada] = [],
            [EstadoDaProposta.Cancelada] = [],
            [EstadoDaProposta.Expirada] = [],
        }.ToFrozenDictionary();

    public static IReadOnlyList<EstadoDaProposta> SaidasDe(EstadoDaProposta estado) => Transicoes[estado];

    public static bool PodeTransitar(EstadoDaProposta de, EstadoDaProposta para) =>
        Array.IndexOf(Transicoes[de], para) >= 0;

    /// <summary>Portao unico por onde toda mudanca de estado passa.</summary>
    public static void GarantirTransicao(EstadoDaProposta de, EstadoDaProposta para)
    {
        if (!PodeTransitar(de, para))
        {
            throw new TransicaoInvalidaException(de, para);
        }
    }

    public static bool EhTerminal(EstadoDaProposta estado) => Transicoes[estado].Length == 0;
}
