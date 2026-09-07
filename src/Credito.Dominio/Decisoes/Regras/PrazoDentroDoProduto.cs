namespace Credito.Dominio.Decisoes.Regras;

public sealed class PrazoDentroDoProduto : IRegraDeCredito
{
    public const string CodigoDaRegra = "PRAZO_DENTRO_DO_PRODUTO";

    public string Codigo => CodigoDaRegra;

    public VeredictoDaRegra Avaliar(ContextoDaAnalise contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var politica = contexto.Politica;
        var pedido = contexto.PrazoEmMeses;

        if (pedido < politica.PrazoMinimoEmMeses)
        {
            return Reprovar(
                pedido,
                politica.PrazoMinimoEmMeses,
                $"Prazo de {TextoDoLaudo.Meses(pedido)} abaixo do minimo de {TextoDoLaudo.Meses(politica.PrazoMinimoEmMeses)}.");
        }

        if (pedido > politica.PrazoMaximoEmMeses)
        {
            return Reprovar(
                pedido,
                politica.PrazoMaximoEmMeses,
                $"Prazo de {TextoDoLaudo.Meses(pedido)} acima do maximo de {TextoDoLaudo.Meses(politica.PrazoMaximoEmMeses)}.");
        }

        return new VeredictoDaRegra(
            CodigoDaRegra,
            Aprovou: true,
            $"Prazo de {TextoDoLaudo.Meses(pedido)} dentro da faixa do produto "
            + $"({TextoDoLaudo.Meses(politica.PrazoMinimoEmMeses)} a {TextoDoLaudo.Meses(politica.PrazoMaximoEmMeses)}).",
            pedido);
    }

    private static VeredictoDaRegra Reprovar(int observado, int limite, string motivo) =>
        new(CodigoDaRegra, Aprovou: false, motivo, observado, limite);
}
