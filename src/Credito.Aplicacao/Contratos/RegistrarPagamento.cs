using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Contratos;
using Credito.Dominio.Erros;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Contratos;

public sealed class RegistrarPagamento
{
    private const string Origem = "api:pagamento";

    private readonly IRepositorioDeContratos contratos;
    private readonly IRepositorioDePropostas propostas;
    private readonly IContaBancaria banco;
    private readonly IUnidadeDeTrabalho unidade;
    private readonly TimeProvider relogio;

    public RegistrarPagamento(
        IRepositorioDeContratos contratos,
        IRepositorioDePropostas propostas,
        IContaBancaria banco,
        IUnidadeDeTrabalho unidade,
        TimeProvider relogio)
    {
        this.contratos = contratos;
        this.propostas = propostas;
        this.banco = banco;
        this.unidade = unidade;
        this.relogio = relogio;
    }

    public async Task<PagamentoRegistrado> Executar(
        Guid propostaId,
        int numero,
        string chaveDoPagamento,
        CancellationToken cancelamento)
    {
        var contrato = await contratos.PorProposta(propostaId, cancelamento).ConfigureAwait(false)
            ?? throw new ContratoNaoEncontradoException(propostaId);

        var agora = relogio.GetUtcNow();

        // A cobranca vem antes, e a gravacao daqui so acontece no fim: enquanto o banco nao
        // confirmar, nada foi salvo, e um debito recusado por saldo deixa a parcela em
        // aberto. Salvar antes de cobrar quitaria a parcela sem ninguem ter pago.
        var cobranca = await Cobrar(contrato, numero, cancelamento).ConfigureAwait(false);

        var parcela = contrato.Pagar(numero, agora, chaveDoPagamento);

        var proposta = await propostas.PorId(propostaId, cancelamento).ConfigureAwait(false)
            ?? throw new PropostaNaoEncontradaException(propostaId);

        // A liquidacao nao e um pedido a parte: ela acontece quando a ultima parcela cai.
        // A conferencia do estado deixa o reenvio do mesmo pagamento inofensivo — a segunda
        // vez encontra o contrato ja quitado e a proposta ja liquidada, e nao faz nada.
        if (contrato.EstaQuitado && proposta.Estado == EstadoDaProposta.Contratada)
        {
            proposta.Liquidar(contrato, agora, Origem);
        }

        await unidade.Salvar(cancelamento).ConfigureAwait(false);

        return new PagamentoRegistrado(
            propostaId,
            parcela.Numero,
            parcela.PagaEm!.Value,
            parcela.Valor,
            contrato.TotalPago,
            contrato.SaldoAberto,
            contrato.EstaQuitado,
            proposta.Estado,
            cobranca?.Id,
            cobranca?.SaldoDepois);
    }

    /// <summary>
    /// Debita a parcela na conta, quando o contrato tem conta.
    /// </summary>
    /// <remarks>
    /// Contrato sem conta continua valendo: a parcela e liquidada por fora e aqui so se
    /// registra que foi paga. O que nao pode e cobrar parcela de um contrato que tem conta
    /// e nunca desembolsou — seria cobrar por um emprestimo que o cliente nao recebeu.
    /// </remarks>
    private async Task<LancamentoNaConta?> Cobrar(
        Contrato contrato,
        int numero,
        CancellationToken cancelamento)
    {
        if (contrato.ContaId is not { } contaId)
        {
            return null;
        }

        if (!contrato.EstaDesembolsado)
        {
            throw new ContratoInvalidoException(
                "O contrato tem conta e ainda nao foi desembolsado; nao ha o que cobrar.");
        }

        // Numero inexistente e recusado aqui, antes de o dinheiro se mexer.
        var parcela = contrato.Parcela(numero);

        return await banco.Debitar(
            new PedidoDeLancamentoNaConta(
                contaId,
                parcela.Valor,
                $"Parcela {numero} do contrato {contrato.Id:N}",
                contrato.ChaveDaParcela(numero),
                Origem),
            cancelamento).ConfigureAwait(false);
    }
}
