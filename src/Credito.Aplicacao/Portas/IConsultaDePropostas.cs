using Credito.Aplicacao.Consultas;

namespace Credito.Aplicacao.Portas;

/// <summary>
/// O lado de leitura em lote. Nao passa pelo agregado de proposito.
/// </summary>
/// <remarks>
/// Carregar <see cref="Credito.Dominio.Propostas.Proposta"/> para listar traria historico,
/// laudo e avaliacoes junto — tres colecoes filhas por linha, num produto cartesiano que
/// vira centenas de linhas de banco para exibir vinte. E nada disso seria usado: listagem
/// nao tem comportamento, so mostra. Aqui a consulta projeta direto para o formato da
/// resposta, e o agregado continua sendo o unico caminho de escrita.
/// </remarks>
public interface IConsultaDePropostas
{
    Task<IReadOnlyList<LinhaDaProposta>> Listar(FiltroDeLeitura filtro, CancellationToken cancelamento);

    /// <summary>Contagem e volume por estado, agrupados no banco.</summary>
    Task<IReadOnlyList<ContagemPorEstado>> Resumir(
        DateTimeOffset? de,
        DateTimeOffset? ate,
        CancellationToken cancelamento);
}
