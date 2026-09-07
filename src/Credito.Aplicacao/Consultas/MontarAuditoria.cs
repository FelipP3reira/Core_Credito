using System.Globalization;
using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Contratos;
using Credito.Dominio.Decisoes;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Consultas;

public sealed class MontarAuditoria
{
    // Mesma razao da cultura fixa no laudo: texto de auditoria nao pode mudar de formato
    // porque o servidor subiu com outra configuracao regional.
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IRepositorioDePropostas propostas;
    private readonly IRepositorioDeContratos contratos;
    private readonly IProtetorDeCpf protetor;

    public MontarAuditoria(
        IRepositorioDePropostas propostas,
        IRepositorioDeContratos contratos,
        IProtetorDeCpf protetor)
    {
        this.propostas = propostas;
        this.contratos = contratos;
        this.protetor = protetor;
    }

    public async Task<TrilhaDeAuditoria> Executar(Guid id, CancellationToken cancelamento)
    {
        var proposta = await propostas.PorId(id, cancelamento).ConfigureAwait(false)
            ?? throw new PropostaNaoEncontradaException(id);

        var contrato = await contratos.PorProposta(id, cancelamento).ConfigureAwait(false);

        return new TrilhaDeAuditoria(
            proposta.Id,
            proposta.Estado,
            proposta.NomeSolicitante,
            protetor.Revelar(proposta.Cpf).Mascarado,
            Ordenar(Reunir(proposta, contrato)));
    }

    /// <remarks>
    /// O relogio sozinho nao ordena: uma requisicao so faz varias coisas no mesmo instante
    /// — a analise transita duas vezes e grava o laudo, e o pagamento da ultima parcela
    /// liquida a proposta. O empate se resolve por <see cref="Prioridade"/> e, dentro dela,
    /// pela ordem em que os eventos foram produzidos.
    /// </remarks>
    private static IReadOnlyList<EventoDaTrilha> Ordenar(IEnumerable<EventoDaTrilha> eventos) =>
        [.. eventos
            .Select((evento, indice) => (evento, indice))
            .OrderBy(par => par.evento.OcorridoEm)
            .ThenBy(par => Prioridade(par.evento.Tipo))
            .ThenBy(par => par.indice)
            .Select(par => par.evento)];

    /// <summary>
    /// Desempate de eventos simultaneos: primeiro o que provocou a mudanca, depois a
    /// mudanca, por ultimo o que a justifica.
    /// </summary>
    /// <remarks>
    /// Sem isso a trilha mostra a proposta sendo liquidada antes do pagamento que a
    /// liquidou, e o contrato aparecendo depois da transicao para Contratada — os dois
    /// acontecem no mesmo instante, e a leitura sai de tras para frente.
    /// </remarks>
    private static int Prioridade(string tipo) => tipo switch
    {
        TiposDeEvento.Contrato or TiposDeEvento.Pagamento => 0,
        TiposDeEvento.Estado => 1,
        _ => 2,
    };

    private static IEnumerable<EventoDaTrilha> Reunir(Proposta proposta, Contrato? contrato)
    {
        foreach (var transicao in proposta.Transicoes.OrderBy(linha => linha.Sequencia))
        {
            yield return new EventoDaTrilha(
                transicao.OcorridaEm,
                TiposDeEvento.Estado,
                $"{transicao.De} para {transicao.Para}",
                transicao.Origem);

            if (LaudoDa(proposta, transicao) is not { } decisao)
            {
                continue;
            }

            foreach (var evento in DoLaudo(decisao, transicao.Origem))
            {
                yield return evento;
            }
        }

        if (contrato is null)
        {
            yield break;
        }

        foreach (var evento in DoContrato(contrato))
        {
            yield return evento;
        }
    }

    // O laudo entra logo depois da transicao que ele justificou, e nao no fim da trilha.
    private static Decisao? LaudoDa(Proposta proposta, TransicaoDeEstado transicao) =>
        transicao.Para is EstadoDaProposta.Aprovada or EstadoDaProposta.Negada
            ? proposta.Decisoes.FirstOrDefault(linha => linha.AvaliadaEm == transicao.OcorridaEm)
            : null;

    private static IEnumerable<EventoDaTrilha> DoLaudo(Decisao decisao, string origem)
    {
        var conclusao = decisao.Aprovada ? "Aprovada" : "Negada";
        var taxa = (decisao.TaxaMensalAplicada * 100).ToString("0.##", Brasil);

        yield return new EventoDaTrilha(
            decisao.AvaliadaEm,
            TiposDeEvento.Decisao,
            $"{conclusao} pela politica versao {decisao.VersaoDaPolitica}, score {decisao.ScoreObservado}, taxa {taxa}% ao mes",
            origem);

        // Todas as regras, e nao so as que reprovaram: sem as que passaram nao da para
        // saber o que chegou a ser conferido.
        foreach (var avaliacao in decisao.Avaliacoes.OrderBy(linha => linha.Ordem))
        {
            var veredicto = avaliacao.Aprovou ? "passou" : "falhou";

            yield return new EventoDaTrilha(
                decisao.AvaliadaEm,
                TiposDeEvento.Regra,
                $"{avaliacao.Codigo} {veredicto}: {avaliacao.Motivo}",
                origem);
        }
    }

    private static IEnumerable<EventoDaTrilha> DoContrato(Contrato contrato)
    {
        var financiado = contrato.ValorFinanciado.ToString("C2", Brasil);
        var taxa = (contrato.TaxaMensal * 100).ToString("0.##", Brasil);

        yield return new EventoDaTrilha(
            contrato.AssinadoEm,
            TiposDeEvento.Contrato,
            $"Contrato de {financiado} em {contrato.PrazoEmMeses} parcelas pela {contrato.Sistema}, taxa {taxa}% ao mes",
            "api:contratacao");

        var pagas = contrato.Parcelas
            .Where(parcela => parcela.PagaEm is not null)
            .OrderBy(parcela => parcela.PagaEm)
            .ThenBy(parcela => parcela.Numero);

        foreach (var parcela in pagas)
        {
            var valor = parcela.Valor.ToString("C2", Brasil);

            yield return new EventoDaTrilha(
                parcela.PagaEm!.Value,
                TiposDeEvento.Pagamento,
                $"Parcela {parcela.Numero} de {contrato.PrazoEmMeses} paga: {valor}",
                "api:pagamento");
        }
    }
}
