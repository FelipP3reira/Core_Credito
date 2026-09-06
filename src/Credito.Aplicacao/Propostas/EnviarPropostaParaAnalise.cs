using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Propostas;

public sealed class EnviarPropostaParaAnalise
{
    private const string Origem = "api:submissao";

    private readonly IRepositorioDePropostas repositorio;
    private readonly TimeProvider relogio;

    public EnviarPropostaParaAnalise(IRepositorioDePropostas repositorio, TimeProvider relogio)
    {
        this.repositorio = repositorio;
        this.relogio = relogio;
    }

    public async Task<EstadoDaProposta> Executar(Guid id, CancellationToken cancelamento)
    {
        var proposta = await repositorio.PorId(id, cancelamento).ConfigureAwait(false)
            ?? throw new PropostaNaoEncontradaException(id);

        proposta.EnviarParaAnalise(relogio.GetUtcNow(), Origem);
        await repositorio.Salvar(cancelamento).ConfigureAwait(false);

        return proposta.Estado;
    }
}
