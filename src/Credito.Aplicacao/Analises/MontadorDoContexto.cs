using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Amortizacao;
using Credito.Dominio.Decisoes;
using Credito.Dominio.Erros;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Analises;

/// <summary>
/// Primeira fase da analise: junta tudo que as regras precisam e congela.
/// </summary>
/// <remarks>
/// A ordem aqui nao e arbitraria e nao pode ser invertida: a taxa sai da faixa de score,
/// que sai do birô; e o cronograma precisa da taxa. Era essa cadeia que faria as regras
/// dependerem umas das outras se cada uma fosse buscar o que precisa por conta.
/// </remarks>
public sealed class MontadorDoContexto
{
    private readonly IRepositorioDePoliticas politicas;
    private readonly IConsultaDeBureau bureau;
    private readonly IProtetorDeCpf protetor;
    private readonly SistemasDeAmortizacao sistemas;
    private readonly TimeProvider relogio;

    public MontadorDoContexto(
        IRepositorioDePoliticas politicas,
        IConsultaDeBureau bureau,
        IProtetorDeCpf protetor,
        SistemasDeAmortizacao sistemas,
        TimeProvider relogio)
    {
        this.politicas = politicas;
        this.bureau = bureau;
        this.protetor = protetor;
        this.sistemas = sistemas;
        this.relogio = relogio;
    }

    public async Task<ContextoDaAnalise> Montar(Proposta proposta, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(proposta);

        var politica = await politicas.Vigente(relogio.GetUtcNow(), cancelamento).ConfigureAwait(false)
            ?? throw new PoliticaVigenteAusenteException();

        // Unico ponto do fluxo em que o CPF volta a texto claro, porque o birô precisa dele.
        var informacao = await bureau
            .Consultar(protetor.Revelar(proposta.Cpf), cancelamento)
            .ConfigureAwait(false);

        var taxa = politica.TaxaPara(informacao.Score);

        return new ContextoDaAnalise(
            proposta.Id,
            proposta.ValorSolicitado,
            proposta.PrazoEmMeses,
            proposta.RendaMensal,
            informacao.Score,
            informacao.TemRestricao,
            taxa,
            TentarMontarCronograma(proposta, taxa),
            politica);
    }

    /// <summary>
    /// Devolve nulo quando valor, taxa e prazo nao se representam em centavos.
    /// </summary>
    /// <remarks>
    /// Deixar a excecao subir derrubaria a analise inteira com 400, e proposta impossivel
    /// merece laudo dizendo isso — nao erro de servidor. A regra de comprometimento e quem
    /// transforma o nulo em reprovacao explicada.
    /// </remarks>
    private Cronograma? TentarMontarCronograma(Proposta proposta, decimal taxaMensal)
    {
        try
        {
            return sistemas
                .De(proposta.Sistema)
                .Gerar(proposta.ValorSolicitado, taxaMensal, proposta.PrazoEmMeses);
        }
        catch (AmortizacaoInvalidaException)
        {
            return null;
        }
    }
}
