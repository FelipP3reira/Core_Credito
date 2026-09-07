using Credito.Dominio.Decisoes;
using Credito.Dominio.Decisoes.Regras;
using Credito.Dominio.Erros;

namespace Credito.Testes.Unidade.Decisoes;

public class MotorDeDecisaoTestes
{
    /// <summary>Regra de teste com veredicto fixo, que anota se chegaram a chama-la.</summary>
    private sealed class RegraCombinada : IRegraDeCredito
    {
        private readonly bool aprova;

        public RegraCombinada(string codigo, bool aprova)
        {
            Codigo = codigo;
            this.aprova = aprova;
        }

        public string Codigo { get; }

        public bool FoiAvaliada { get; private set; }

        public VeredictoDaRegra Avaliar(ContextoDaAnalise contexto)
        {
            FoiAvaliada = true;
            return new VeredictoDaRegra(Codigo, aprova, aprova ? "passou" : "reprovou");
        }
    }

    /// <summary>
    /// O teste que justifica o desenho inteiro. Se o motor parasse na primeira reprovacao,
    /// o laudo registraria uma causa quando existiam tres — e quem corrigisse so aquela
    /// voltaria a ser negado sem entender por que.
    /// </summary>
    [Fact]
    public void RodaTodasAsRegrasMesmoDepoisDaPrimeiraReprovacao()
    {
        var primeira = new RegraCombinada("PRIMEIRA", aprova: false);
        var segunda = new RegraCombinada("SEGUNDA", aprova: false);
        var terceira = new RegraCombinada("TERCEIRA", aprova: true);

        var resultado = new MotorDeDecisao([primeira, segunda, terceira])
            .Avaliar(ContextoDeExemplo.Contexto());

        Assert.True(primeira.FoiAvaliada);
        Assert.True(segunda.FoiAvaliada);
        Assert.True(terceira.FoiAvaliada);
        Assert.Equal(3, resultado.Veredictos.Count);
        Assert.Equal(2, resultado.Veredictos.Count(veredicto => !veredicto.Aprovou));
    }

    [Fact]
    public void AprovaApenasQuandoNenhumaRegraReprovou()
    {
        var contexto = ContextoDeExemplo.Contexto();

        Assert.True(new MotorDeDecisao([
            new RegraCombinada("A", true),
            new RegraCombinada("B", true),
        ]).Avaliar(contexto).Aprovada);

        Assert.False(new MotorDeDecisao([
            new RegraCombinada("A", true),
            new RegraCombinada("B", false),
        ]).Avaliar(contexto).Aprovada);
    }

    // O laudo precisa sair sempre na mesma ordem para poder ser comparado entre analises.
    [Fact]
    public void PreservaAOrdemDeRegistroDasRegras()
    {
        var resultado = new MotorDeDecisao([
            new RegraCombinada("TERCEIRA", true),
            new RegraCombinada("PRIMEIRA", true),
            new RegraCombinada("SEGUNDA", true),
        ]).Avaliar(ContextoDeExemplo.Contexto());

        Assert.Equal(
            ["TERCEIRA", "PRIMEIRA", "SEGUNDA"],
            resultado.Veredictos.Select(veredicto => veredicto.Codigo));
    }

    // Duas linhas com o mesmo nome e conclusoes diferentes deixariam o laudo ambiguo.
    [Fact]
    public void RecusaDuasRegrasComOMesmoCodigo() =>
        Assert.Throws<PoliticaInvalidaException>(() => new MotorDeDecisao([
            new RegraCombinada("REPETIDA", true),
            new RegraCombinada("REPETIDA", false),
        ]));

    [Fact]
    public void RecusaMotorSemRegra() =>
        Assert.Throws<PoliticaInvalidaException>(() => new MotorDeDecisao([]));

    [Fact]
    public void ComOConjuntoRealAprovaSolicitanteFolgado()
    {
        var resultado = MotorReal().Avaliar(ContextoDeExemplo.Contexto(score: 820, rendaMensal: 12_000m));

        Assert.True(resultado.Aprovada);
        Assert.Equal(5, resultado.Veredictos.Count);
        Assert.All(resultado.Veredictos, veredicto => Assert.True(veredicto.Aprovou));
    }

    [Fact]
    public void ComOConjuntoRealRegistraTodasAsCausasDeUmaVez()
    {
        var resultado = MotorReal().Avaliar(ContextoDeExemplo.Contexto(
            valorSolicitado: 150_000m,
            prazoEmMeses: 120,
            rendaMensal: 1_500m,
            score: 300,
            temRestricaoCadastral: true));

        Assert.False(resultado.Aprovada);
        Assert.Equal(
            [
                RestricaoCadastral.CodigoDaRegra,
                ScoreMinimo.CodigoDaRegra,
                ValorDentroDoProduto.CodigoDaRegra,
                PrazoDentroDoProduto.CodigoDaRegra,
                ComprometimentoDeRenda.CodigoDaRegra,
            ],
            resultado.Veredictos.Where(veredicto => !veredicto.Aprovou).Select(veredicto => veredicto.Codigo));
    }

    private static MotorDeDecisao MotorReal() => new([
        new RestricaoCadastral(),
        new ScoreMinimo(),
        new ValorDentroDoProduto(),
        new PrazoDentroDoProduto(),
        new ComprometimentoDeRenda(),
    ]);
}
