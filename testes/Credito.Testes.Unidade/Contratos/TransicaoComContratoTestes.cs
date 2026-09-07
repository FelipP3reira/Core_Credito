using Credito.Dominio.Amortizacao;
using Credito.Dominio.Contratos;
using Credito.Dominio.Decisoes;
using Credito.Dominio.Decisoes.Regras;
using Credito.Dominio.Erros;
using Credito.Dominio.Propostas;
using Credito.Testes.Unidade.Decisoes;
using Credito.Testes.Unidade.Propostas;

namespace Credito.Testes.Unidade.Contratos;

public class TransicaoComContratoTestes
{
    private static readonly MotorDeDecisao Motor = new([
        new RestricaoCadastral(),
        new ScoreMinimo(),
        new ValorDentroDoProduto(),
        new PrazoDentroDoProduto(),
        new ComprometimentoDeRenda(),
    ]);

    private static Proposta Aprovada()
    {
        var proposta = Proposta.Rascunho(DadosDeExemplo.Validos(), DadosDeExemplo.Agora);
        proposta.EnviarParaAnalise(DadosDeExemplo.Agora, "teste");

        var contexto = ContextoDeExemplo.Contexto(score: 820, rendaMensal: 12_000m) with
        {
            PropostaId = proposta.Id,
        };

        proposta.Aprovar(
            Decisao.Registrar(contexto, Motor.Avaliar(contexto), DadosDeExemplo.Agora),
            DadosDeExemplo.Agora,
            "motor:decisao");

        return proposta;
    }

    private static Contrato ContratoDe(Proposta proposta, int prazoEmMeses = 6) =>
        Contrato.Assinar(
            proposta.Id,
            new TabelaPrice().Gerar(10_000m, 0.019m, prazoEmMeses),
            DateOnly.FromDateTime(DadosDeExemplo.Agora.UtcDateTime).AddDays(30),
            DadosDeExemplo.Agora);

    private static Contrato Quitado(Proposta proposta)
    {
        var contrato = ContratoDe(proposta);

        foreach (var parcela in contrato.Parcelas)
        {
            contrato.Pagar(parcela.Numero, DadosDeExemplo.Agora, $"chave-{parcela.Numero}");
        }

        return contrato;
    }

    [Fact]
    public void ContratarLevaDeAprovadaParaContratada()
    {
        var proposta = Aprovada();

        proposta.Contratar(ContratoDe(proposta), DadosDeExemplo.Agora, "api:contratacao");

        Assert.Equal(EstadoDaProposta.Contratada, proposta.Estado);
        Assert.Equal(EstadoDaProposta.Contratada, proposta.Transicoes[^1].Para);
    }

    [Fact]
    public void ContratoDeOutraPropostaEhRecusado()
    {
        var proposta = Aprovada();
        var outra = Aprovada();

        Assert.Throws<PropostaInvalidaException>(
            () => proposta.Contratar(ContratoDe(outra), DadosDeExemplo.Agora, "api:contratacao"));

        Assert.Equal(EstadoDaProposta.Aprovada, proposta.Estado);
    }

    [Fact]
    public void PropostaQueNemFoiAprovadaNaoContrata()
    {
        var proposta = Proposta.Rascunho(DadosDeExemplo.Validos(), DadosDeExemplo.Agora);

        Assert.Throws<TransicaoInvalidaException>(
            () => proposta.Contratar(ContratoDe(proposta), DadosDeExemplo.Agora, "api:contratacao"));

        Assert.Empty(proposta.Transicoes);
    }

    [Fact]
    public void LiquidarExigeContratoQuitado()
    {
        var proposta = Aprovada();
        var contrato = ContratoDe(proposta);
        proposta.Contratar(contrato, DadosDeExemplo.Agora, "api:contratacao");

        // Uma parcela paga de seis nao quita nada.
        contrato.Pagar(1, DadosDeExemplo.Agora, "chave-1");

        var erro = Assert.Throws<PropostaInvalidaException>(
            () => proposta.Liquidar(contrato, DadosDeExemplo.Agora, "api:pagamento"));

        Assert.Contains("em aberto", erro.Message, StringComparison.Ordinal);
        Assert.Equal(EstadoDaProposta.Contratada, proposta.Estado);
    }

    [Fact]
    public void LiquidarComTudoPagoLevaParaLiquidada()
    {
        var proposta = Aprovada();
        var contrato = ContratoDe(proposta);
        proposta.Contratar(contrato, DadosDeExemplo.Agora, "api:contratacao");

        foreach (var parcela in contrato.Parcelas)
        {
            contrato.Pagar(parcela.Numero, DadosDeExemplo.Agora, $"chave-{parcela.Numero}");
        }

        proposta.Liquidar(contrato, DadosDeExemplo.Agora, "api:pagamento");

        Assert.Equal(EstadoDaProposta.Liquidada, proposta.Estado);
        Assert.Equal(
            [
                EstadoDaProposta.EmAnalise,
                EstadoDaProposta.Aprovada,
                EstadoDaProposta.Contratada,
                EstadoDaProposta.Liquidada,
            ],
            proposta.Transicoes.Select(transicao => transicao.Para));
    }

    [Fact]
    public void ContratoQuitadoDeOutraPropostaNaoLiquida()
    {
        var proposta = Aprovada();
        var outra = Aprovada();
        proposta.Contratar(ContratoDe(proposta), DadosDeExemplo.Agora, "api:contratacao");

        Assert.Throws<PropostaInvalidaException>(
            () => proposta.Liquidar(Quitado(outra), DadosDeExemplo.Agora, "api:pagamento"));
    }

    [Fact]
    public void ExpirarLevaDeAprovadaParaExpirada()
    {
        var proposta = Aprovada();

        proposta.Expirar(DadosDeExemplo.Agora, "api:contratacao");

        Assert.Equal(EstadoDaProposta.Expirada, proposta.Estado);
    }

    // Contrato assinado nao expira: a partir dali a divida existe.
    [Fact]
    public void PropostaJaContratadaNaoExpira()
    {
        var proposta = Aprovada();
        proposta.Contratar(ContratoDe(proposta), DadosDeExemplo.Agora, "api:contratacao");

        Assert.Throws<TransicaoInvalidaException>(
            () => proposta.Expirar(DadosDeExemplo.Agora, "api:contratacao"));
    }

    [Fact]
    public void PropostaExpiradaNaoContrataMais()
    {
        var proposta = Aprovada();
        proposta.Expirar(DadosDeExemplo.Agora, "api:contratacao");

        Assert.Throws<TransicaoInvalidaException>(
            () => proposta.Contratar(ContratoDe(proposta), DadosDeExemplo.Agora, "api:contratacao"));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(29, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void ValidadeDaAprovacaoSegueAPolitica(int diasDepois, bool aindaVale)
    {
        var politica = ContextoDeExemplo.Politica(validadeDaAprovacaoEmDias: 30);
        var aprovadaEm = DadosDeExemplo.Agora;

        Assert.Equal(aindaVale, politica.AprovacaoAindaVale(aprovadaEm, aprovadaEm.AddDays(diasDepois)));
    }
}
