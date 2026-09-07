namespace Credito.Aplicacao.Portas;

/// <param name="ChaveIdempotencia">
/// Derivada do contrato, nunca sorteada. E ela que faz uma tentativa repetida encontrar o
/// lancamento que ja existe em vez de criar um segundo.
/// </param>
/// <param name="Operador">Quem mandou fazer, para a auditoria do outro lado.</param>
public sealed record PedidoDeLancamentoNaConta(
    Guid ContaId,
    decimal Valor,
    string Descricao,
    string ChaveIdempotencia,
    string Operador);

/// <param name="Novo">
/// Falso quando o banco reconheceu a chave e devolveu o lancamento que ja tinha.
/// </param>
public sealed record LancamentoNaConta(
    Guid Id,
    long Sequencia,
    decimal SaldoDepois,
    DateTimeOffset CriadoEm,
    bool Novo);

/// <summary>
/// A conta bancaria do cliente, na Plataforma Bancaria.
/// </summary>
/// <remarks>
/// Dois servicos, e nao um banco compartilhado: o credito nao enxerga a tabela de
/// lancamentos do outro lado, so pede credito e debito pela borda publica dele. Por isso
/// nao existe transacao cobrindo os dois — o que existe e chave de idempotencia derivada,
/// que deixa cada pedido ser repetido ate a resposta chegar.
/// </remarks>
public interface IContaBancaria
{
    /// <summary>Poe dinheiro na conta. E por aqui que o emprestimo chega ao cliente.</summary>
    Task<LancamentoNaConta> Creditar(PedidoDeLancamentoNaConta pedido, CancellationToken cancelamento);

    /// <summary>Tira dinheiro da conta. E por aqui que a parcela e cobrada.</summary>
    Task<LancamentoNaConta> Debitar(PedidoDeLancamentoNaConta pedido, CancellationToken cancelamento);
}
