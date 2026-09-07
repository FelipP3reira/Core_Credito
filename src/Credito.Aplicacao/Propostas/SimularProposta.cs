using Credito.Aplicacao.Analises;
using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Erros;

namespace Credito.Aplicacao.Propostas;

/// <summary>
/// Monta o cronograma que a proposta teria com a taxa da politica vigente.
/// </summary>
/// <remarks>
/// A taxa nao vem mais de fora: sai da faixa de score da politica, igual a que a analise
/// usaria. Simulacao que aceitasse qualquer taxa mostraria uma parcela que o motor jamais
/// ofereceria.
/// <para>
/// Nao muda estado nem grava nada, e por isso vale em qualquer estado da proposta. O
/// cronograma so vira dado persistido na contratacao.
/// </para>
/// </remarks>
public sealed class SimularProposta
{
    private readonly IRepositorioDePropostas repositorio;
    private readonly MontadorDoContexto montador;

    public SimularProposta(IRepositorioDePropostas repositorio, MontadorDoContexto montador)
    {
        this.repositorio = repositorio;
        this.montador = montador;
    }

    public async Task<SimulacaoDaProposta> Executar(Guid id, CancellationToken cancelamento)
    {
        var proposta = await repositorio.PorId(id, cancelamento).ConfigureAwait(false)
            ?? throw new PropostaNaoEncontradaException(id);

        var contexto = await montador.Montar(proposta, cancelamento).ConfigureAwait(false);

        // Aqui, ao contrario da analise, cronograma impossivel e erro do pedido: quem
        // simula quer o cronograma, e nao ha laudo para explicar a ausencia dele.
        var cronograma = contexto.Cronograma
            ?? throw new AmortizacaoInvalidaException(
                "Valor, taxa e prazo nao fecham em centavos: o cronograma nao existe para esta proposta.");

        return new SimulacaoDaProposta(
            proposta.Id,
            cronograma.ValorFinanciado,
            cronograma.TaxaMensal,
            proposta.PrazoEmMeses,
            cronograma.Sistema,
            contexto.Score,
            cronograma.PrimeiraParcela,
            cronograma.UltimaParcela,
            cronograma.TotalPago,
            cronograma.TotalDeJuros,
            [.. cronograma.Parcelas.Select(parcela => new ParcelaSimulada(
                parcela.Numero,
                parcela.Amortizacao,
                parcela.Juros,
                parcela.Valor,
                parcela.SaldoDevedor))]);
    }
}
