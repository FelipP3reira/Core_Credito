namespace Credito.Dominio.Decisoes.Regras;

public sealed class ValorDentroDoProduto : IRegraDeCredito
{
    public const string CodigoDaRegra = "VALOR_DENTRO_DO_PRODUTO";

    public string Codigo => CodigoDaRegra;

    public VeredictoDaRegra Avaliar(ContextoDaAnalise contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var politica = contexto.Politica;
        var pedido = contexto.ValorSolicitado;

        if (pedido < politica.ValorMinimo)
        {
            return Reprovar(
                pedido,
                politica.ValorMinimo,
                $"Valor de {TextoDoLaudo.Dinheiro(pedido)} abaixo do minimo de {TextoDoLaudo.Dinheiro(politica.ValorMinimo)}.");
        }

        if (pedido > politica.ValorMaximo)
        {
            return Reprovar(
                pedido,
                politica.ValorMaximo,
                $"Valor de {TextoDoLaudo.Dinheiro(pedido)} acima do maximo de {TextoDoLaudo.Dinheiro(politica.ValorMaximo)}.");
        }

        return new VeredictoDaRegra(
            CodigoDaRegra,
            Aprovou: true,
            $"Valor de {TextoDoLaudo.Dinheiro(pedido)} dentro da faixa do produto "
            + $"({TextoDoLaudo.Dinheiro(politica.ValorMinimo)} a {TextoDoLaudo.Dinheiro(politica.ValorMaximo)}).",
            pedido);
    }

    private static VeredictoDaRegra Reprovar(decimal observado, decimal limite, string motivo) =>
        new(CodigoDaRegra, Aprovou: false, motivo, observado, limite);
}
