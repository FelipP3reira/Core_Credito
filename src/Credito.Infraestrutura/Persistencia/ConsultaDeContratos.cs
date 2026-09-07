using Credito.Aplicacao.Consultas;
using Credito.Aplicacao.Portas;
using Microsoft.EntityFrameworkCore;

namespace Credito.Infraestrutura.Persistencia;

/// <summary>
/// Os contratos de uma conta, com o cronograma resumido no banco.
/// </summary>
/// <remarks>
/// As somas de parcela — total pago, saldo aberto, quantas caíram — saem de subconsultas
/// agregadas, e nao de trazer as parcelas para memoria e somar aqui. A diferenca aparece
/// justamente no caso que importa: uma conta com contrato de noventa e seis parcelas
/// devolve uma linha em vez de noventa e seis.
/// </remarks>
public sealed class ConsultaDeContratos : IConsultaDeContratos
{
    private readonly ContextoDeCredito contexto;

    public ConsultaDeContratos(ContextoDeCredito contexto) => this.contexto = contexto;

    public async Task<IReadOnlyList<ContratoDaConta>> DaConta(
        Guid contaId,
        CancellationToken cancelamento)
    {
        var linhas = await contexto.Contratos
            .AsNoTracking()
            .Where(contrato => contrato.ContaId == contaId)
            .OrderByDescending(contrato => contrato.AssinadoEm)
            .Select(contrato => new
            {
                contrato.Id,
                contrato.PropostaId,
                contrato.ValorFinanciado,
                contrato.TaxaMensal,
                contrato.Sistema,
                contrato.PrazoEmMeses,
                contrato.AssinadoEm,
                contrato.DesembolsadoEm,

                TotalPago = contrato.Parcelas
                    .Where(parcela => parcela.PagaEm != null)
                    .Sum(parcela => (decimal?)parcela.Valor) ?? 0m,

                SaldoAberto = contrato.Parcelas
                    .Where(parcela => parcela.PagaEm == null)
                    .Sum(parcela => (decimal?)parcela.Valor) ?? 0m,

                ParcelasPagas = contrato.Parcelas.Count(parcela => parcela.PagaEm != null),

                // A proxima e a de menor numero ainda em aberto — e nao a de menor
                // vencimento. Sao a mesma coisa hoje, mas quem paga uma parcela fora de
                // ordem nao pode fazer o banco cobrar de novo uma que ja caiu.
                Proxima = contrato.Parcelas
                    .Where(parcela => parcela.PagaEm == null)
                    .OrderBy(parcela => parcela.Numero)
                    .Select(parcela => new
                    {
                        parcela.Numero,
                        parcela.Vencimento,
                        parcela.Valor,
                    })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancelamento)
            .ConfigureAwait(false);

        return
        [
            .. linhas.Select(linha => new ContratoDaConta(
                linha.Id,
                linha.PropostaId,
                linha.ValorFinanciado,
                linha.TaxaMensal,
                linha.Sistema,
                linha.PrazoEmMeses,
                linha.TotalPago,
                linha.SaldoAberto,
                linha.ParcelasPagas,
                linha.ParcelasPagas == linha.PrazoEmMeses,
                linha.AssinadoEm,
                linha.DesembolsadoEm,
                new ProximaParcela(
                    linha.Proxima?.Numero,
                    linha.Proxima?.Vencimento,
                    linha.Proxima?.Valor))),
        ];
    }
}
