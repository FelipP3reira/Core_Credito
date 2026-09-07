namespace Credito.Dominio.Decisoes.Regras;

public sealed class ScoreMinimo : IRegraDeCredito
{
    public const string CodigoDaRegra = "SCORE_MINIMO";

    public string Codigo => CodigoDaRegra;

    public VeredictoDaRegra Avaliar(ContextoDaAnalise contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var exigido = contexto.Politica.ScoreMinimo;
        var alcancou = contexto.Score >= exigido;

        return new VeredictoDaRegra(
            Codigo,
            alcancou,
            alcancou
                ? $"Score {contexto.Score} atende o minimo de {exigido}."
                : $"Score {contexto.Score} abaixo do minimo de {exigido}.",
            contexto.Score,
            exigido);
    }
}
