using Credito.Aplicacao.Analises;
using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Propostas;

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

    // A maquina de estados so permite uma decisao por proposta; a mais recente e a unica.
    private static LaudoDaDecisao? Laudo(Proposta proposta)
    {
        var decisao = proposta.Decisoes.OrderByDescending(linha => linha.AvaliadaEm).FirstOrDefault();

        return decisao is null
            ? null
            : new LaudoDaDecisao(
                decisao.Aprovada,
                decisao.ScoreObservado,
                decisao.TaxaMensalAplicada,
                decisao.VersaoDaPolitica,
                decisao.AvaliadaEm,
                [.. decisao.Avaliacoes
                    .OrderBy(avaliacao => avaliacao.Ordem)
                    .Select(avaliacao => new LinhaDoLaudo(
                        avaliacao.Codigo,
                        avaliacao.Aprovou,
                        avaliacao.Motivo,
                        avaliacao.ValorObservado,
                        avaliacao.LimiteExigido))]);
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
                .OrderBy(transicao => transicao.Sequencia)
                .Select(transicao => new MudancaDeEstado(
                    transicao.Sequencia,
                    transicao.De,
                    transicao.Para,
                    transicao.OcorridaEm,
                    transicao.Origem))],
            Laudo(proposta));
    }
}
