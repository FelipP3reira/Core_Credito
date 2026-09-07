using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Propostas;

/// <summary>
/// Desiste da proposta antes de haver decisao.
/// </summary>
/// <remarks>
/// So vale enquanto nada foi decidido. Depois de aprovada ou negada existe laudo, e
/// cancelar apagaria o motivo de uma decisao ja tomada — para aprovacao que o solicitante
/// nao quer mais, o caminho e deixar expirar.
/// </remarks>
public sealed class CancelarProposta
{
    private const string Origem = "api:cancelamento";

    private readonly IRepositorioDePropostas propostas;
    private readonly IUnidadeDeTrabalho unidade;
    private readonly TimeProvider relogio;

    public CancelarProposta(
        IRepositorioDePropostas propostas,
        IUnidadeDeTrabalho unidade,
        TimeProvider relogio)
    {
        this.propostas = propostas;
        this.unidade = unidade;
        this.relogio = relogio;
    }

    public async Task<EstadoDaProposta> Executar(Guid id, CancellationToken cancelamento)
    {
        var proposta = await propostas.PorId(id, cancelamento).ConfigureAwait(false)
            ?? throw new PropostaNaoEncontradaException(id);

        proposta.Cancelar(relogio.GetUtcNow(), Origem);
        await unidade.Salvar(cancelamento).ConfigureAwait(false);

        return proposta.Estado;
    }
}
