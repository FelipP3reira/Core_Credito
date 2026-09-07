using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Erros;

namespace Credito.Aplicacao.Contratos;

/// <param name="Novo">
/// Falso quando o banco reconheceu a chave: o desembolso ja tinha acontecido e esta
/// resposta so confirma o que existe.
/// </param>
public sealed record DesembolsoRegistrado(
    Guid PropostaId,
    Guid ContratoId,
    Guid ContaId,
    decimal Valor,
    Guid LancamentoId,
    decimal SaldoDaConta,
    DateTimeOffset DesembolsadoEm,
    bool Novo);

/// <summary>
/// Poe o valor financiado na conta do cliente.
/// </summary>
/// <remarks>
/// Operacao a parte da contratacao, e nao um passo dentro dela. Sao dois sistemas com
/// bancos de dados proprios: nao existe transacao que cubra os dois, e fingir que existe
/// so esconderia o problema. O desenho assume que a chamada pode falhar no meio e resolve
/// isso deixando o pedido ser repetido — a chave e derivada do contrato, entao a segunda
/// tentativa encontra o lancamento da primeira em vez de creditar de novo.
/// <para>
/// A ordem importa: primeiro o banco, depois a gravacao daqui. Se a gravacao falhar, o
/// contrato fica marcado como nao desembolsado e uma nova tentativa reapresenta a mesma
/// chave — o banco devolve o mesmo lancamento e a marcacao se acerta. Na ordem inversa, um
/// contrato marcado como desembolsado sem dinheiro nenhum na conta seria o resultado, e
/// nada no sistema apontaria para isso.
/// </para>
/// </remarks>
public sealed class DesembolsarContrato
{
    private const string Operador = "credito:desembolso";

    private readonly IRepositorioDeContratos contratos;
    private readonly IContaBancaria banco;
    private readonly IUnidadeDeTrabalho unidade;
    private readonly TimeProvider relogio;

    public DesembolsarContrato(
        IRepositorioDeContratos contratos,
        IContaBancaria banco,
        IUnidadeDeTrabalho unidade,
        TimeProvider relogio)
    {
        this.contratos = contratos;
        this.banco = banco;
        this.unidade = unidade;
        this.relogio = relogio;
    }

    public async Task<DesembolsoRegistrado> Executar(Guid propostaId, CancellationToken cancelamento)
    {
        var contrato = await contratos.PorProposta(propostaId, cancelamento).ConfigureAwait(false)
            ?? throw new ContratoNaoEncontradoException(propostaId);

        if (contrato.ContaId is not { } contaId)
        {
            throw new ContratoInvalidoException("O contrato nao tem conta para receber o desembolso.");
        }

        var chave = contrato.ChaveDeDesembolso();

        // Mesmo ja desembolsado, o pedido vai ao banco: com a chave derivada, ele devolve o
        // lancamento que ja criou. E o que deixa esta rota responder o saldo de verdade em
        // vez de um resumo montado de memoria.
        var lancamento = await banco.Creditar(
            new PedidoDeLancamentoNaConta(
                contaId,
                contrato.ValorFinanciado,
                $"Desembolso do contrato {contrato.Id:N}",
                chave,
                Operador),
            cancelamento).ConfigureAwait(false);

        contrato.Desembolsar(chave, lancamento.Id, relogio.GetUtcNow());
        await unidade.Salvar(cancelamento).ConfigureAwait(false);

        return new DesembolsoRegistrado(
            propostaId,
            contrato.Id,
            contaId,
            contrato.ValorFinanciado,
            lancamento.Id,
            lancamento.SaldoDepois,
            contrato.DesembolsadoEm!.Value,
            lancamento.Novo);
    }
}
