using Credito.Dominio.Erros;

namespace Credito.Dominio.Politicas;

/// <summary>
/// Os limiares que o motor de decisao aplica, versionados.
/// </summary>
/// <remarks>
/// Versionar nao e refinamento: e o que mantem decisao antiga explicavel. Mudar o score
/// minimo no mes que vem tornaria toda negativa passada incompreensivel se a decisao
/// nao guardasse sob qual versao foi tomada — e explicar decisao antiga e exatamente o
/// que a auditoria vem cobrar.
/// </remarks>
public sealed class PoliticaDeCredito
{
    public const int ScoreMaximoPossivel = 1000;

    private readonly List<FaixaDeTaxa> faixas = [];

    private PoliticaDeCredito()
    {
    }

    public PoliticaDeCredito(
        int versao,
        int scoreMinimo,
        decimal comprometimentoMaximo,
        decimal valorMinimo,
        decimal valorMaximo,
        int prazoMinimoEmMeses,
        int prazoMaximoEmMeses,
        int validadeDaAprovacaoEmDias,
        DateTimeOffset vigenteDesde,
        IEnumerable<FaixaDeTaxa> faixasDeTaxa)
    {
        ArgumentNullException.ThrowIfNull(faixasDeTaxa);

        Id = Guid.CreateVersion7();
        Versao = versao;
        ScoreMinimo = scoreMinimo;
        ComprometimentoMaximo = comprometimentoMaximo;
        ValorMinimo = valorMinimo;
        ValorMaximo = valorMaximo;
        PrazoMinimoEmMeses = prazoMinimoEmMeses;
        PrazoMaximoEmMeses = prazoMaximoEmMeses;
        ValidadeDaAprovacaoEmDias = validadeDaAprovacaoEmDias;
        VigenteDesde = vigenteDesde;
        faixas = [.. faixasDeTaxa.OrderBy(faixa => faixa.ScoreMinimo)];

        Validar();
    }

    public Guid Id { get; private set; }

    public int Versao { get; private set; }

    public int ScoreMinimo { get; private set; }

    /// <summary>Fracao da renda que a parcela pode comprometer: 0,30 e trinta por cento.</summary>
    public decimal ComprometimentoMaximo { get; private set; }

    public decimal ValorMinimo { get; private set; }

    public decimal ValorMaximo { get; private set; }

    public int PrazoMinimoEmMeses { get; private set; }

    public int PrazoMaximoEmMeses { get; private set; }

    /// <summary>
    /// Por quantos dias uma aprovacao continua contratavel.
    /// </summary>
    /// <remarks>
    /// Taxa aprovada tem validade: contratar hoje uma aprovacao de seis meses atras seria
    /// conceder credito com condicao que ninguem mais ofereceria.
    /// </remarks>
    public int ValidadeDaAprovacaoEmDias { get; private set; }

    public DateTimeOffset VigenteDesde { get; private set; }

    public IReadOnlyList<FaixaDeTaxa> Faixas => faixas;

    /// <summary>
    /// Taxa da faixa que cobre o score. Como as faixas cobrem a escala inteira, sempre ha
    /// uma — inclusive para score abaixo do minimo, que recebe a taxa da pior faixa e e
    /// reprovado pela regra de score, nao pela falta de taxa.
    /// </summary>
    /// <summary>A aprovacao ainda esta dentro do prazo de contratacao?</summary>
    public bool AprovacaoAindaVale(DateTimeOffset aprovadaEm, DateTimeOffset agora) =>
        agora <= aprovadaEm.AddDays(ValidadeDaAprovacaoEmDias);

    public decimal TaxaPara(int score) =>
        faixas.FirstOrDefault(faixa => faixa.Cobre(score))?.TaxaMensal
        ?? throw new PoliticaInvalidaException($"Nenhuma faixa de taxa cobre o score {score}.");

    private void Validar()
    {
        if (Versao < 1)
        {
            throw new PoliticaInvalidaException("Versao da politica precisa comecar em 1.");
        }

        if (ScoreMinimo < 0 || ScoreMinimo > ScoreMaximoPossivel)
        {
            throw new PoliticaInvalidaException(
                $"Score minimo precisa ficar entre 0 e {ScoreMaximoPossivel}.");
        }

        if (ComprometimentoMaximo <= 0 || ComprometimentoMaximo > 1)
        {
            throw new PoliticaInvalidaException(
                "Comprometimento maximo precisa ser uma fracao entre zero e um.");
        }

        if (ValorMinimo <= 0 || ValorMaximo < ValorMinimo)
        {
            throw new PoliticaInvalidaException("Faixa de valor do produto invertida ou nao positiva.");
        }

        if (PrazoMinimoEmMeses < 1 || PrazoMaximoEmMeses < PrazoMinimoEmMeses)
        {
            throw new PoliticaInvalidaException("Faixa de prazo do produto invertida ou menor que um mes.");
        }

        if (ValidadeDaAprovacaoEmDias < 1)
        {
            throw new PoliticaInvalidaException("A validade da aprovacao precisa ser de ao menos um dia.");
        }

        GarantirQueAsFaixasCobremAEscalaInteira();
    }

    /// <summary>
    /// Buraco ou sobreposicao entre faixas viraria proposta sem taxa — ou com duas — no
    /// meio da analise, quando ja e tarde para reclamar.
    /// </summary>
    private void GarantirQueAsFaixasCobremAEscalaInteira()
    {
        if (faixas.Count == 0)
        {
            throw new PoliticaInvalidaException("Politica precisa de ao menos uma faixa de taxa.");
        }

        var proximoEsperado = 0;

        foreach (var faixa in faixas)
        {
            if (faixa.ScoreMinimo != proximoEsperado)
            {
                throw new PoliticaInvalidaException(
                    $"As faixas de taxa precisam ser contiguas: esperava faixa comecando em {proximoEsperado}, veio {faixa.ScoreMinimo}.");
            }

            if (faixa.ScoreMaximo < faixa.ScoreMinimo)
            {
                throw new PoliticaInvalidaException("Faixa de taxa com limites invertidos.");
            }

            if (faixa.TaxaMensal < 0)
            {
                throw new PoliticaInvalidaException("Taxa da faixa nao pode ser negativa.");
            }

            proximoEsperado = faixa.ScoreMaximo + 1;
        }

        if (proximoEsperado != ScoreMaximoPossivel + 1)
        {
            throw new PoliticaInvalidaException(
                $"As faixas de taxa precisam cobrir de 0 a {ScoreMaximoPossivel}.");
        }
    }
}
