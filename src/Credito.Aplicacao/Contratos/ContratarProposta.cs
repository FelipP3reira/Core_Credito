using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Amortizacao;
using Credito.Dominio.Contratos;
using Credito.Dominio.Decisoes;
using Credito.Dominio.Erros;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Contratos;

public sealed class ContratarProposta
{
    private const string Origem = "api:contratacao";
    private const int DiasPadraoAteOPrimeiroVencimento = 30;

    private readonly IRepositorioDePropostas propostas;
    private readonly IRepositorioDeContratos contratos;
    private readonly IRepositorioDePoliticas politicas;
    private readonly SistemasDeAmortizacao sistemas;
    private readonly IUnidadeDeTrabalho unidade;
    private readonly TimeProvider relogio;

    public ContratarProposta(
        IRepositorioDePropostas propostas,
        IRepositorioDeContratos contratos,
        IRepositorioDePoliticas politicas,
        SistemasDeAmortizacao sistemas,
        IUnidadeDeTrabalho unidade,
        TimeProvider relogio)
    {
        this.propostas = propostas;
        this.contratos = contratos;
        this.politicas = politicas;
        this.sistemas = sistemas;
        this.unidade = unidade;
        this.relogio = relogio;
    }

    /// <param name="contaId">
    /// A conta que vai receber o desembolso. Opcional: sem ela o contrato existe do mesmo
    /// jeito e a liquidacao acontece por fora.
    /// </param>
    public async Task<DetalheDoContrato> Executar(
        Guid propostaId,
        DateOnly? primeiroVencimento,
        Guid? contaId,
        CancellationToken cancelamento)
    {
        var proposta = await propostas.PorId(propostaId, cancelamento).ConfigureAwait(false)
            ?? throw new PropostaNaoEncontradaException(propostaId);

        var agora = relogio.GetUtcNow();
        var aprovacao = Aprovacao(proposta.Decisoes);

        await GarantirQueAAprovacaoAindaVale(proposta, aprovacao, agora, cancelamento).ConfigureAwait(false);

        // Taxa da DECISAO, nao da politica de hoje. A proposta foi aprovada sob uma
        // condicao e e essa condicao que se contrata; politica que mudou no meio do
        // caminho valeria para a proxima analise, nao para esta aprovacao.
        var cronograma = sistemas
            .De(proposta.Sistema)
            .Gerar(proposta.ValorSolicitado, aprovacao.TaxaMensalAplicada, proposta.PrazoEmMeses);

        var contrato = Contrato.Assinar(
            proposta.Id,
            cronograma,
            primeiroVencimento ?? DateOnly.FromDateTime(agora.UtcDateTime).AddDays(DiasPadraoAteOPrimeiroVencimento),
            agora,
            contaId);

        proposta.Contratar(contrato, agora, Origem);
        contratos.Adicionar(contrato);

        await unidade.Salvar(cancelamento).ConfigureAwait(false);

        return DetalheDoContrato.De(contrato);
    }

    private static Decisao Aprovacao(IReadOnlyList<Decisao> decisoes) =>
        decisoes.OrderByDescending(decisao => decisao.AvaliadaEm).FirstOrDefault(decisao => decisao.Aprovada)
            ?? throw new ContratoInvalidoException("A proposta nao tem aprovacao para contratar.");

    /// <summary>
    /// Aprovacao vencida expira a proposta em vez de so recusar a contratacao.
    /// </summary>
    /// <remarks>
    /// A expiracao acontece aqui, na hora em que alguem tenta usar a aprovacao, e nao por
    /// varredura periodica. E o momento em que a diferenca importa, e evita um processo de
    /// fundo cuja unica funcao seria mudar um estado que ninguem estava olhando.
    /// </remarks>
    private async Task GarantirQueAAprovacaoAindaVale(
        Proposta proposta,
        Decisao aprovacao,
        DateTimeOffset agora,
        CancellationToken cancelamento)
    {
        var politica = await politicas.Vigente(agora, cancelamento).ConfigureAwait(false)
            ?? throw new PoliticaVigenteAusenteException();

        if (politica.AprovacaoAindaVale(aprovacao.AvaliadaEm, agora))
        {
            return;
        }

        proposta.Expirar(agora, Origem);
        await unidade.Salvar(cancelamento).ConfigureAwait(false);

        throw new ContratoInvalidoException(
            $"A aprovacao venceu depois de {politica.ValidadeDaAprovacaoEmDias} dias. E preciso analisar de novo.");
    }
}
