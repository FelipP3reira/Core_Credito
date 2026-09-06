using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Amortizacao;

namespace Credito.Aplicacao.Propostas;

/// <summary>
/// Monta o cronograma de uma proposta com a taxa informada.
/// </summary>
/// <remarks>
/// A taxa vem de fora por enquanto. Quando o motor de decisao existir, ela sai da faixa
/// de score da politica vigente e este caso de uso deixa de aceita-la como argumento.
/// <para>
/// A simulacao nao muda estado nem grava nada: e projecao do que aconteceria, e por isso
/// vale em qualquer estado da proposta. O cronograma so vira dado persistido na
/// contratacao.
/// </para>
/// </remarks>
public sealed class SimularProposta
{
    private readonly IRepositorioDePropostas repositorio;
    private readonly SistemasDeAmortizacao sistemas;

    public SimularProposta(IRepositorioDePropostas repositorio, SistemasDeAmortizacao sistemas)
    {
        this.repositorio = repositorio;
        this.sistemas = sistemas;
    }

    public async Task<SimulacaoDaProposta> Executar(Guid id, decimal taxaMensal, CancellationToken cancelamento)
    {
        var proposta = await repositorio.PorId(id, cancelamento).ConfigureAwait(false)
            ?? throw new PropostaNaoEncontradaException(id);

        var cronograma = sistemas
            .De(proposta.Sistema)
            .Gerar(proposta.ValorSolicitado, taxaMensal, proposta.PrazoEmMeses);

        return new SimulacaoDaProposta(
            proposta.Id,
            cronograma.ValorFinanciado,
            cronograma.TaxaMensal,
            proposta.PrazoEmMeses,
            cronograma.Sistema,
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
