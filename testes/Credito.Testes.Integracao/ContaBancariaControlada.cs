using System.Collections.Concurrent;
using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;

namespace Credito.Testes.Integracao;

/// <summary>
/// A Plataforma Bancaria vista de fora, com o comportamento que importa aqui.
/// </summary>
/// <remarks>
/// Substituto, e nao a API de verdade: subir o outro servico dentro desta suite faria cada
/// teste de credito depender de um segundo container e de um segundo banco. O que este
/// dublê precisa reproduzir fielmente e o que o credito depende — a chave de idempotencia
/// devolvendo o mesmo lancamento, e a recusa por saldo — e e exatamente isso que ele faz.
/// A conversa por HTTP de verdade e conferida a parte, com os dois servicos no ar.
/// </remarks>
public sealed class ContaBancariaControlada : IContaBancaria
{
    private readonly ConcurrentDictionary<string, LancamentoNaConta> porChave = new();
    private readonly ConcurrentDictionary<Guid, decimal> saldos = new();
    private long sequencia;

    /// <summary>Quantas vezes o credito de fato pediu movimentacao.</summary>
    public int Chamadas { get; private set; }

    /// <summary>Quando ligado, toda movimentacao e recusada como o banco recusaria.</summary>
    public string? Recusa { get; set; }

    /// <summary>Quando ligado, o banco nao responde.</summary>
    public bool ForaDoAr { get; set; }

    public decimal SaldoDe(Guid contaId) => saldos.GetValueOrDefault(contaId);

    /// <summary>Poe dinheiro na conta sem passar pelo credito, como faria o salario.</summary>
    public void Depositar(Guid contaId, decimal valor) =>
        saldos[contaId] = saldos.GetValueOrDefault(contaId) + valor;

    public void Reiniciar()
    {
        porChave.Clear();
        saldos.Clear();
        Chamadas = 0;
        Recusa = null;
        ForaDoAr = false;
        sequencia = 0;
    }

    public Task<LancamentoNaConta> Creditar(
        PedidoDeLancamentoNaConta pedido,
        CancellationToken cancelamento) =>
        Movimentar(pedido, somar: true);

    public Task<LancamentoNaConta> Debitar(
        PedidoDeLancamentoNaConta pedido,
        CancellationToken cancelamento) =>
        Movimentar(pedido, somar: false);

    private Task<LancamentoNaConta> Movimentar(PedidoDeLancamentoNaConta pedido, bool somar)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        Chamadas++;

        if (ForaDoAr)
        {
            throw new ContaBancariaIndisponivelException("A plataforma bancaria nao respondeu.");
        }

        // A chave vence tudo, inclusive a recusa: reenvio de algo ja gravado devolve o que
        // foi gravado, e nao uma recusa nova. E assim que o banco de verdade responde.
        if (porChave.TryGetValue(pedido.ChaveIdempotencia, out var existente))
        {
            return Task.FromResult(existente with { Novo = false });
        }

        if (Recusa is { } motivo)
        {
            throw new ContaBancariaRecusouException(motivo);
        }

        var saldo = saldos.GetValueOrDefault(pedido.ContaId);

        if (!somar && saldo < pedido.Valor)
        {
            throw new ContaBancariaRecusouException(
                $"Saldo de {saldo} nao cobre o debito de {pedido.Valor}.");
        }

        var depois = somar ? saldo + pedido.Valor : saldo - pedido.Valor;
        saldos[pedido.ContaId] = depois;

        var lancamento = new LancamentoNaConta(
            Guid.CreateVersion7(),
            Interlocked.Increment(ref sequencia),
            depois,
            DateTimeOffset.UnixEpoch,
            Novo: true);

        porChave[pedido.ChaveIdempotencia] = lancamento;

        return Task.FromResult(lancamento);
    }
}
