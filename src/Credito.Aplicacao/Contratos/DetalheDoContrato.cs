using Credito.Dominio.Amortizacao;
using Credito.Dominio.Contratos;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Contratos;

public sealed record ParcelaDoContrato(
    int Numero,
    DateOnly Vencimento,
    decimal Amortizacao,
    decimal Juros,
    decimal Valor,
    decimal SaldoDevedor,
    DateTimeOffset? PagaEm);

public sealed record DetalheDoContrato(
    Guid Id,
    Guid PropostaId,
    Guid? ContaId,
    DateTimeOffset? DesembolsadoEm,
    decimal ValorFinanciado,
    decimal TaxaMensal,
    SistemaDeAmortizacao Sistema,
    int PrazoEmMeses,
    DateOnly PrimeiroVencimento,
    DateTimeOffset AssinadoEm,
    decimal TotalPago,
    decimal SaldoAberto,
    bool EstaQuitado,
    IReadOnlyList<ParcelaDoContrato> Parcelas)
{
    public static DetalheDoContrato De(Contrato contrato)
    {
        ArgumentNullException.ThrowIfNull(contrato);

        return new DetalheDoContrato(
            contrato.Id,
            contrato.PropostaId,
            contrato.ContaId,
            contrato.DesembolsadoEm,
            contrato.ValorFinanciado,
            contrato.TaxaMensal,
            contrato.Sistema,
            contrato.PrazoEmMeses,
            contrato.PrimeiroVencimento,
            contrato.AssinadoEm,
            contrato.TotalPago,
            contrato.SaldoAberto,
            contrato.EstaQuitado,
            [.. contrato.Parcelas
                .OrderBy(parcela => parcela.Numero)
                .Select(parcela => new ParcelaDoContrato(
                    parcela.Numero,
                    parcela.Vencimento,
                    parcela.Amortizacao,
                    parcela.Juros,
                    parcela.Valor,
                    parcela.SaldoDevedor,
                    parcela.PagaEm))]);
    }
}

public sealed record PagamentoRegistrado(
    Guid PropostaId,
    int Numero,
    DateTimeOffset PagaEm,
    decimal Valor,
    decimal TotalPago,
    decimal SaldoAberto,
    bool ContratoQuitado,
    EstadoDaProposta EstadoDaProposta,
    Guid? LancamentoNaConta,
    decimal? SaldoDaConta);
