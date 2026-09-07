using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Contratos;

public sealed class RegistrarPagamento
{
    private const string Origem = "api:pagamento";

    private readonly IRepositorioDeContratos contratos;
    private readonly IRepositorioDePropostas propostas;
    private readonly IUnidadeDeTrabalho unidade;
    private readonly TimeProvider relogio;

    public RegistrarPagamento(
        IRepositorioDeContratos contratos,
        IRepositorioDePropostas propostas,
        IUnidadeDeTrabalho unidade,
        TimeProvider relogio)
    {
        this.contratos = contratos;
        this.propostas = propostas;
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
            proposta.Estado);
    }
}
