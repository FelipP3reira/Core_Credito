using Credito.Dominio.Erros;

namespace Credito.Dominio.Decisoes;

public sealed record ResultadoDaAnalise(bool Aprovada, IReadOnlyList<VeredictoDaRegra> Veredictos);

public sealed class MotorDeDecisao
{
    private readonly IRegraDeCredito[] regras;

    public MotorDeDecisao(IEnumerable<IRegraDeCredito> regras)
    {
        ArgumentNullException.ThrowIfNull(regras);

        this.regras = [.. regras];

        if (this.regras.Length == 0)
        {
            throw new PoliticaInvalidaException("O motor precisa de ao menos uma regra.");
        }

        // Codigo repetido tornaria o laudo ambiguo: duas linhas com o mesmo nome e
        // conclusoes diferentes, sem como saber qual delas pesou.
        var repetido = this.regras
            .GroupBy(regra => regra.Codigo, StringComparer.Ordinal)
            .FirstOrDefault(grupo => grupo.Count() > 1);

        if (repetido is not null)
        {
            throw new PoliticaInvalidaException($"Ha mais de uma regra com o codigo {repetido.Key}.");
        }
    }

    /// <summary>
    /// Roda TODAS as regras e aprova apenas se nenhuma reprovou.
    /// </summary>
    /// <remarks>
    /// Sem atalho de propósito. Parar na primeira reprovacao seria mais rapido e gravaria
    /// uma causa quando existiam tres — e o solicitante que corrigisse so aquela voltaria
    /// a ser negado, sem entender por que. A auditoria completa e o motivo de o sistema
    /// existir; economizar avaliacao de regra em memoria nao paga esse preco.
    /// </remarks>
    public ResultadoDaAnalise Avaliar(ContextoDaAnalise contexto)
    {
        var veredictos = Array.ConvertAll(regras, regra => regra.Avaliar(contexto));

        return new ResultadoDaAnalise(Array.TrueForAll(veredictos, veredicto => veredicto.Aprovou), veredictos);
    }
}
