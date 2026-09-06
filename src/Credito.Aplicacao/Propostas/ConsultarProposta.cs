using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;

namespace Credito.Aplicacao.Propostas;

public sealed class ConsultarProposta
{
    private readonly IRepositorioDePropostas repositorio;
    private readonly IProtetorDeCpf protetor;

    public ConsultarProposta(IRepositorioDePropostas repositorio, IProtetorDeCpf protetor)
    {
        this.repositorio = repositorio;
        this.protetor = protetor;
    }

    public async Task<DetalheDaProposta> Executar(Guid id, CancellationToken cancelamento)
    {
        var proposta = await repositorio.PorId(id, cancelamento).ConfigureAwait(false)
            ?? throw new PropostaNaoEncontradaException(id);

        // Decifra so para mascarar em seguida. Parece rodeio, mas evita guardar uma
        // terceira coluna com a mascara e ter duas versoes do mesmo dado no banco.
        // A renda fica de fora da resposta enquanto nao houver papel de usuario.
        return new DetalheDaProposta(
            proposta.Id,
            proposta.Estado,
            proposta.NomeSolicitante,
            protetor.Revelar(proposta.Cpf).Mascarado,
            proposta.ValorSolicitado,
            proposta.PrazoEmMeses,
            proposta.Sistema,
            proposta.CriadaEm,
            proposta.AtualizadaEm,
            [.. proposta.Transicoes
                .OrderBy(transicao => transicao.OcorridaEm)
                .Select(transicao => new MudancaDeEstado(
                    transicao.De, transicao.Para, transicao.OcorridaEm, transicao.Origem))]);
    }
}
