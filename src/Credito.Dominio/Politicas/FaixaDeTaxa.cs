namespace Credito.Dominio.Politicas;

/// <summary>
/// Taxa cobrada para uma janela de score. Limites inclusivos nas duas pontas.
/// </summary>
public sealed class FaixaDeTaxa
{
    private FaixaDeTaxa()
    {
    }

    public FaixaDeTaxa(int scoreMinimo, int scoreMaximo, decimal taxaMensal)
    {
        Id = Guid.CreateVersion7();
        ScoreMinimo = scoreMinimo;
        ScoreMaximo = scoreMaximo;
        TaxaMensal = taxaMensal;
    }

    public Guid Id { get; private set; }

    public Guid PoliticaId { get; private set; }

    public int ScoreMinimo { get; private set; }

    public int ScoreMaximo { get; private set; }

    /// <summary>Fracao ao mes: 0,0189 e 1,89% a.m.</summary>
    public decimal TaxaMensal { get; private set; }

    public bool Cobre(int score) => score >= ScoreMinimo && score <= ScoreMaximo;
}
