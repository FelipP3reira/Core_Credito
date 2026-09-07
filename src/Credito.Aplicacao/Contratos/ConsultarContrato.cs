using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;

namespace Credito.Aplicacao.Contratos;

public sealed class ConsultarContrato
{
    private readonly IRepositorioDeContratos contratos;

    public ConsultarContrato(IRepositorioDeContratos contratos) => this.contratos = contratos;

    public async Task<DetalheDoContrato> Executar(Guid propostaId, CancellationToken cancelamento) =>
        DetalheDoContrato.De(
            await contratos.PorProposta(propostaId, cancelamento).ConfigureAwait(false)
            ?? throw new ContratoNaoEncontradoException(propostaId));
}
