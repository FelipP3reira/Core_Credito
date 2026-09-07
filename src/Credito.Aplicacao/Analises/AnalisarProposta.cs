using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Decisoes;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Analises;

public sealed class AnalisarProposta
{
    private const string OrigemDaSubmissao = "api:submissao";
    private const string OrigemDaDecisao = "motor:decisao";

    private readonly IRepositorioDePropostas repositorio;
    private readonly MontadorDoContexto montador;
    private readonly MotorDeDecisao motor;
    private readonly IUnidadeDeTrabalho unidade;
    private readonly TimeProvider relogio;

    public AnalisarProposta(
        IRepositorioDePropostas repositorio,
        MontadorDoContexto montador,
        MotorDeDecisao motor,
        IUnidadeDeTrabalho unidade,
        TimeProvider relogio)
    {
        this.repositorio = repositorio;
        this.montador = montador;
        this.motor = motor;
        this.unidade = unidade;
        this.relogio = relogio;
    }

    public async Task<ResultadoDaDecisao> Executar(Guid id, CancellationToken cancelamento)
    {
        var proposta = await repositorio.PorId(id, cancelamento).ConfigureAwait(false)
            ?? throw new PropostaNaoEncontradaException(id);

        var agora = relogio.GetUtcNow();

        // Estado terminal para por aqui, antes de qualquer consulta externa: quem barra e
        // a maquina de estados, nao uma conferencia repetida deste lado.
        if (proposta.Estado != EstadoDaProposta.EmAnalise)
        {
            proposta.EnviarParaAnalise(agora, OrigemDaSubmissao);
        }

        var contexto = await montador.Montar(proposta, cancelamento).ConfigureAwait(false);
        var resultado = motor.Avaliar(contexto);
        var decisao = Decisao.Registrar(contexto, resultado, agora);

        if (resultado.Aprovada)
        {
            proposta.Aprovar(decisao, agora, OrigemDaDecisao);
        }
        else
        {
            proposta.Negar(decisao, agora, OrigemDaDecisao);
        }

        // Uma gravacao so: as duas transicoes, o laudo e o estado novo entram juntos ou
        // nao entram. Proposta aprovada sem laudo gravado seria o pior resultado possivel.
        await unidade.Salvar(cancelamento).ConfigureAwait(false);

        return new ResultadoDaDecisao(
            proposta.Id,
            proposta.Estado,
            decisao.Aprovada,
            decisao.ScoreObservado,
            decisao.TaxaMensalAplicada,
            decisao.VersaoDaPolitica,
            decisao.AvaliadaEm,
            [.. decisao.Avaliacoes.Select(avaliacao => new LinhaDoLaudo(
                avaliacao.Codigo,
                avaliacao.Aprovou,
                avaliacao.Motivo,
                avaliacao.ValorObservado,
                avaliacao.LimiteExigido))]);
    }
}
