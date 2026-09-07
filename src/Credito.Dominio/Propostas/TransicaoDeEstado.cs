namespace Credito.Dominio.Propostas;

/// <summary>
/// Uma linha por mudanca de estado, gravada na mesma transacao da mudanca.
/// Registro imutavel: nao existe caminho para alterar ou apagar.
/// </summary>
public sealed class TransicaoDeEstado
{
    private TransicaoDeEstado()
    {
    }

    internal TransicaoDeEstado(
        Guid propostaId,
        int sequencia,
        EstadoDaProposta de,
        EstadoDaProposta para,
        DateTimeOffset ocorridaEm,
        string origem)
    {
        Id = Guid.CreateVersion7();
        PropostaId = propostaId;
        Sequencia = sequencia;
        De = de;
        Para = para;
        OcorridaEm = ocorridaEm;
        Origem = origem;
    }

    public Guid Id { get; private set; }

    public Guid PropostaId { get; private set; }

    /// <summary>
    /// Posicao na trilha, comecando em 1.
    /// </summary>
    /// <remarks>
    /// Nao da para ordenar so pela data: a analise faz duas transicoes na mesma requisicao,
    /// com o mesmo instante, e a trilha sairia em ordem indefinida — justamente na hora em
    /// que ela precisa ser lida como sequencia.
    /// </remarks>
    public int Sequencia { get; private set; }

    public EstadoDaProposta De { get; private set; }

    public EstadoDaProposta Para { get; private set; }

    public DateTimeOffset OcorridaEm { get; private set; }

    /// <summary>Quem causou a mudanca, ex: "api:submissao", "motor:decisao".</summary>
    public string Origem { get; private set; } = string.Empty;
}
