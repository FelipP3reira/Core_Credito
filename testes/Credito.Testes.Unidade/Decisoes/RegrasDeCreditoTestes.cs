using Credito.Dominio.Decisoes.Regras;

namespace Credito.Testes.Unidade.Decisoes;

/// <summary>
/// Cada regra avaliada sozinha, contra um contexto montado na mao. Sem simulacao de
/// dependencia e sem banco: e o que se ganha por congelar o contexto antes de avaliar.
/// </summary>
public class RegrasDeCreditoTestes
{
    public class RestricaoCadastralTestes
    {
        private static readonly RestricaoCadastral Regra = new();

        [Fact]
        public void ReprovaComRestricaoAtiva()
        {
            var veredicto = Regra.Avaliar(ContextoDeExemplo.Contexto(temRestricaoCadastral: true));

            Assert.False(veredicto.Aprovou);
            Assert.Equal(RestricaoCadastral.CodigoDaRegra, veredicto.Codigo);
            Assert.Contains("restricao cadastral", veredicto.Motivo, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AprovaSemRestricao() =>
            Assert.True(Regra.Avaliar(ContextoDeExemplo.Contexto(temRestricaoCadastral: false)).Aprovou);

        // Regra sem numero nao inventa numero: preencher a coluna com zero atrapalharia
        // quem for consultar o laudo depois.
        [Fact]
        public void NaoTemNumeroParaRegistrar()
        {
            var veredicto = Regra.Avaliar(ContextoDeExemplo.Contexto(temRestricaoCadastral: true));

            Assert.Null(veredicto.ValorObservado);
            Assert.Null(veredicto.LimiteExigido);
        }
    }

    public class ScoreMinimoTestes
    {
        private static readonly ScoreMinimo Regra = new();

        [Theory]
        [InlineData(499, false)]
        [InlineData(500, true)]
        [InlineData(501, true)]
        [InlineData(1000, true)]
        [InlineData(0, false)]
        public void ComparaComOMinimoDaPolitica(int score, bool esperado) =>
            Assert.Equal(esperado, Regra.Avaliar(ContextoDeExemplo.Contexto(score: score)).Aprovou);

        [Fact]
        public void RegistraOScoreObservadoEOExigido()
        {
            var veredicto = Regra.Avaliar(ContextoDeExemplo.Contexto(score: 480));

            Assert.Equal(480m, veredicto.ValorObservado);
            Assert.Equal(500m, veredicto.LimiteExigido);
            Assert.Contains("480", veredicto.Motivo, StringComparison.Ordinal);
            Assert.Contains("500", veredicto.Motivo, StringComparison.Ordinal);
        }

        [Fact]
        public void SegueOMinimoDaPoliticaRecebidaENaoUmValorFixo()
        {
            var frouxa = ContextoDeExemplo.Politica(scoreMinimo: 300);

            Assert.True(Regra.Avaliar(ContextoDeExemplo.Contexto(score: 350, politica: frouxa)).Aprovou);
        }
    }

    public class ValorDentroDoProdutoTestes
    {
        private static readonly ValorDentroDoProduto Regra = new();

        [Theory]
        [InlineData(999, false)]
        [InlineData(1_000, true)]
        [InlineData(50_000, true)]
        [InlineData(100_000, true)]
        [InlineData(100_001, false)]
        public void ComparaComAFaixaDoProduto(decimal valor, bool esperado) =>
            Assert.Equal(
                esperado,
                Regra.Avaliar(ContextoDeExemplo.Contexto(valorSolicitado: valor)).Aprovou);

        [Fact]
        public void DizQualLimiteFoiViolado()
        {
            var abaixo = Regra.Avaliar(ContextoDeExemplo.Contexto(valorSolicitado: 500m));
            var acima = Regra.Avaliar(ContextoDeExemplo.Contexto(valorSolicitado: 200_000m));

            Assert.Contains("abaixo do minimo", abaixo.Motivo, StringComparison.Ordinal);
            Assert.Equal(1_000m, abaixo.LimiteExigido);

            Assert.Contains("acima do maximo", acima.Motivo, StringComparison.Ordinal);
            Assert.Equal(100_000m, acima.LimiteExigido);
        }
    }

    public class PrazoDentroDoProdutoTestes
    {
        private static readonly PrazoDentroDoProduto Regra = new();

        [Theory]
        [InlineData(5, false)]
        [InlineData(6, true)]
        [InlineData(48, true)]
        [InlineData(96, true)]
        [InlineData(97, false)]
        public void ComparaComAFaixaDoProduto(int prazo, bool esperado) =>
            Assert.Equal(esperado, Regra.Avaliar(ContextoDeExemplo.Contexto(prazoEmMeses: prazo)).Aprovou);

        [Fact]
        public void EscreveOPluralCerto()
        {
            var umMes = ContextoDeExemplo.Politica(prazoMinimoEmMeses: 1, prazoMaximoEmMeses: 1);

            var veredicto = Regra.Avaliar(ContextoDeExemplo.Contexto(prazoEmMeses: 1, politica: umMes));

            Assert.Contains("1 mes", veredicto.Motivo, StringComparison.Ordinal);
            Assert.DoesNotContain("1 meses", veredicto.Motivo, StringComparison.Ordinal);
        }
    }

    public class ComprometimentoDeRendaTestes
    {
        private static readonly ComprometimentoDeRenda Regra = new();

        [Fact]
        public void AprovaQuandoAParcelaCabeNaRenda()
        {
            // 20 mil em 24 meses a 1,9% da parcela perto de mil reais, folgada em 8.500.
            var veredicto = Regra.Avaliar(ContextoDeExemplo.Contexto(rendaMensal: 8_500m));

            Assert.True(veredicto.Aprovou);
            Assert.NotNull(veredicto.ValorObservado);
            Assert.Equal(0.30m, veredicto.LimiteExigido);
        }

        [Fact]
        public void ReprovaQuandoAParcelaPesaDemais()
        {
            var veredicto = Regra.Avaliar(ContextoDeExemplo.Contexto(rendaMensal: 2_000m));

            Assert.False(veredicto.Aprovou);
            Assert.Contains("acima do limite", veredicto.Motivo, StringComparison.Ordinal);
        }

        /// <summary>
        /// Olha a PRIMEIRA parcela, nao a media. No SAC a primeira e a mais cara, e e ela
        /// que precisa caber no mes que vem — aprovar pela media aprovaria quem quebra no
        /// primeiro vencimento.
        /// </summary>
        [Fact]
        public void UsaAPrimeiraParcelaQueNoSacEhAMaisCara()
        {
            var contexto = ContextoDeExemplo.Contexto();
            var veredicto = Regra.Avaliar(contexto);

            var esperado = contexto.Cronograma!.PrimeiraParcela / contexto.RendaMensal;

            Assert.Equal(esperado, veredicto.ValorObservado);
        }

        // Sem cronograma nao ha parcela, e sem parcela nao da para dizer que passou.
        [Fact]
        public void ReprovaQuandoNaoFoiPossivelMontarOCronograma()
        {
            var veredicto = Regra.Avaliar(ContextoDeExemplo.Contexto(comCronograma: false));

            Assert.False(veredicto.Aprovou);
            Assert.Null(veredicto.ValorObservado);
            Assert.Contains("cronograma", veredicto.Motivo, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SegueOLimiteDaPoliticaRecebida()
        {
            var apertada = ContextoDeExemplo.Politica(comprometimentoMaximo: 0.05m);

            Assert.False(Regra.Avaliar(ContextoDeExemplo.Contexto(politica: apertada)).Aprovou);
        }
    }
}
