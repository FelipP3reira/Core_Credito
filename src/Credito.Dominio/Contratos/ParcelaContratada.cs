using Credito.Dominio.Erros;

namespace Credito.Dominio.Contratos;

/// <summary>
/// Uma parcela do contrato assinado, com vencimento e situacao de pagamento.
/// </summary>
/// <remarks>
/// Diferente de <see cref="Amortizacao.Parcela"/>, que e o resultado do calculo e nao tem
/// identidade nem existencia no banco. Sao dois conceitos parecidos de proposito: um e a
/// conta, o outro e a divida.
/// </remarks>
public sealed class ParcelaContratada
{
    private ParcelaContratada()
    {
    }

    internal ParcelaContratada(
        Guid contratoId,
        int numero,
        DateOnly vencimento,
        decimal amortizacao,
        decimal juros,
        decimal valor,
        decimal saldoDevedor)
    {
        Id = Guid.CreateVersion7();
        ContratoId = contratoId;
        Numero = numero;
        Vencimento = vencimento;
        Amortizacao = amortizacao;
        Juros = juros;
        Valor = valor;
        SaldoDevedor = saldoDevedor;
    }

    public Guid Id { get; private set; }

    public Guid ContratoId { get; private set; }

    public int Numero { get; private set; }

    public DateOnly Vencimento { get; private set; }

    public decimal Amortizacao { get; private set; }

    public decimal Juros { get; private set; }

    public decimal Valor { get; private set; }

    /// <summary>O que ainda se deve depois desta parcela.</summary>
    public decimal SaldoDevedor { get; private set; }

    public DateTimeOffset? PagaEm { get; private set; }

    /// <summary>
    /// Chave do pagamento que quitou a parcela.
    /// </summary>
    /// <remarks>
    /// Guardada para o reenvio da mesma cobranca ser reconhecido como repeticao. Sem ela,
    /// so daria para responder "ja esta paga" — e o cliente que perdeu a resposta por
    /// timeout nao teria como saber se foi ele mesmo quem pagou.
    /// </remarks>
    public string? ChaveDoPagamento { get; private set; }

    public bool EstaPaga => PagaEm is not null;

    internal void Pagar(DateTimeOffset agora, string chaveDoPagamento)
    {
        if (EstaPaga)
        {
            // Mesma chave e reenvio: nada muda e ninguem reclama. Chave diferente e outro
            // pagamento tentando quitar o que ja foi quitado, e isso precisa doer.
            if (string.Equals(ChaveDoPagamento, chaveDoPagamento, StringComparison.Ordinal))
            {
                return;
            }

            throw new ContratoInvalidoException($"A parcela {Numero} ja foi paga.");
        }

        PagaEm = agora;
        ChaveDoPagamento = chaveDoPagamento;
    }
}
