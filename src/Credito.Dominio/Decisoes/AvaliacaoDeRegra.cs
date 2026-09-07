namespace Credito.Dominio.Decisoes;

/// <summary>
/// Uma linha do laudo: o que a regra concluiu e com base em que numero.
/// Registro imutavel, sem caminho de alteracao nem de exclusao.
/// </summary>
public sealed class AvaliacaoDeRegra
{
    private AvaliacaoDeRegra()
    {
    }

    internal AvaliacaoDeRegra(Guid decisaoId, int ordem, VeredictoDaRegra veredicto)
    {
        Id = Guid.CreateVersion7();
        DecisaoId = decisaoId;
        Ordem = ordem;
        Codigo = veredicto.Codigo;
        Aprovou = veredicto.Aprovou;
        Motivo = veredicto.Motivo;
        ValorObservado = veredicto.ValorObservado;
        LimiteExigido = veredicto.LimiteExigido;
    }

    public Guid Id { get; private set; }

    public Guid DecisaoId { get; private set; }

    /// <summary>Posicao no laudo, para ele sair sempre na mesma ordem.</summary>
    public int Ordem { get; private set; }

    public string Codigo { get; private set; } = string.Empty;

    public bool Aprovou { get; private set; }

    public string Motivo { get; private set; } = string.Empty;

    public decimal? ValorObservado { get; private set; }

    public decimal? LimiteExigido { get; private set; }
}
