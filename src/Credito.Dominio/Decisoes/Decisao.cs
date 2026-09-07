namespace Credito.Dominio.Decisoes;

/// <summary>
/// O laudo de uma analise: o resultado, as condicoes que valeriam e o veredicto de cada
/// regra que participou.
/// </summary>
/// <remarks>
/// Guarda a versao da politica aplicada, e nao so o resultado. Sem isso, mudar o score
/// minimo no mes que vem tornaria toda negativa passada inexplicavel.
/// </remarks>
public sealed class Decisao
{
    private readonly List<AvaliacaoDeRegra> avaliacoes = [];

    private Decisao()
    {
    }

    private Decisao(ContextoDaAnalise contexto, ResultadoDaAnalise resultado, DateTimeOffset avaliadaEm)
    {
        Id = Guid.CreateVersion7();
        PropostaId = contexto.PropostaId;
        Aprovada = resultado.Aprovada;
        ScoreObservado = contexto.Score;
        TaxaMensalAplicada = contexto.TaxaMensalAplicada;
        VersaoDaPolitica = contexto.Politica.Versao;
        AvaliadaEm = avaliadaEm;

        avaliacoes.AddRange(
            resultado.Veredictos.Select((veredicto, ordem) => new AvaliacaoDeRegra(Id, ordem, veredicto)));
    }

    public Guid Id { get; private set; }

    public Guid PropostaId { get; private set; }

    public bool Aprovada { get; private set; }

    public int ScoreObservado { get; private set; }

    public decimal TaxaMensalAplicada { get; private set; }

    public int VersaoDaPolitica { get; private set; }

    public DateTimeOffset AvaliadaEm { get; private set; }

    public IReadOnlyList<AvaliacaoDeRegra> Avaliacoes => avaliacoes;

    public IEnumerable<AvaliacaoDeRegra> Reprovacoes => avaliacoes.Where(avaliacao => !avaliacao.Aprovou);

    public static Decisao Registrar(
        ContextoDaAnalise contexto,
        ResultadoDaAnalise resultado,
        DateTimeOffset avaliadaEm)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(resultado);

        return new Decisao(contexto, resultado, avaliadaEm);
    }
}
