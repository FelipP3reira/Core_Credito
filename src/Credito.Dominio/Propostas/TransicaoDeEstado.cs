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
        EstadoDaProposta de,
        EstadoDaProposta para,
        DateTimeOffset ocorridaEm,
        string origem)
    {
        Id = Guid.CreateVersion7();
        PropostaId = propostaId;
        De = de;
        Para = para;
        OcorridaEm = ocorridaEm;
        Origem = origem;
    }

    public Guid Id { get; private set; }

    public Guid PropostaId { get; private set; }

    public EstadoDaProposta De { get; private set; }

    public EstadoDaProposta Para { get; private set; }

    public DateTimeOffset OcorridaEm { get; private set; }

    /// <summary>Quem causou a mudanca, ex: "api:submissao", "motor:decisao".</summary>
    public string Origem { get; private set; } = string.Empty;
}
