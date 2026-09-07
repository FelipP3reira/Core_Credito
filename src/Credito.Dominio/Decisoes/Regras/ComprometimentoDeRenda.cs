namespace Credito.Dominio.Decisoes.Regras;

/// <summary>
/// Quanto da renda mensal a parcela consome.
/// </summary>
/// <remarks>
/// A regra olha a PRIMEIRA parcela, nao a media. No SAC a primeira e a mais cara, e e ela
/// que o solicitante precisa conseguir pagar no mes que vem — aprovar pela media aprovaria
/// gente que quebra no primeiro vencimento.
/// </remarks>
public sealed class ComprometimentoDeRenda : IRegraDeCredito
{
    public const string CodigoDaRegra = "COMPROMETIMENTO_DE_RENDA";

    public string Codigo => CodigoDaRegra;

    public VeredictoDaRegra Avaliar(ContextoDaAnalise contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var limite = contexto.Politica.ComprometimentoMaximo;

        // Sem cronograma nao ha parcela, e sem parcela nao da para dizer que passou.
        // Reprovar dizendo o motivo e mais honesto que aprovar por omissao.
        if (contexto.Cronograma is null)
        {
            return new VeredictoDaRegra(
                Codigo,
                Aprovou: false,
                "Nao foi possivel montar o cronograma para o valor e o prazo pedidos.",
                LimiteExigido: limite);
        }

        var parcela = contexto.Cronograma.PrimeiraParcela;
        var comprometido = parcela / contexto.RendaMensal;
        var cabe = comprometido <= limite;

        return new VeredictoDaRegra(
            Codigo,
            cabe,
            cabe
                ? $"Parcela de {TextoDoLaudo.Dinheiro(parcela)} compromete {TextoDoLaudo.Percentual(comprometido)} da renda, dentro do limite de {TextoDoLaudo.Percentual(limite)}."
                : $"Parcela de {TextoDoLaudo.Dinheiro(parcela)} compromete {TextoDoLaudo.Percentual(comprometido)} da renda, acima do limite de {TextoDoLaudo.Percentual(limite)}.",
            comprometido,
            limite);
    }
}
