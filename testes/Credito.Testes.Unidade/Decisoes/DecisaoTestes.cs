using Credito.Dominio.Decisoes;
using Credito.Dominio.Decisoes.Regras;
using Credito.Dominio.Erros;
using Credito.Dominio.Propostas;
using Credito.Testes.Unidade.Propostas;

namespace Credito.Testes.Unidade.Decisoes;

public class DecisaoTestes
{
    private static readonly MotorDeDecisao Motor = new([
        new RestricaoCadastral(),
        new ScoreMinimo(),
        new ValorDentroDoProduto(),
        new PrazoDentroDoProduto(),
        new ComprometimentoDeRenda(),
    ]);

    private static Decisao Registrar(ContextoDaAnalise contexto) =>
        Decisao.Registrar(contexto, Motor.Avaliar(contexto), ContextoDeExemplo.Agora);

    /// <summary>
    /// Guardar a versao da politica e o que mantem a decisao explicavel depois. Sem isso,
    /// mudar o score minimo no mes que vem tornaria toda negativa passada incompreensivel.
    /// </summary>
    [Fact]
    public void GuardaAsCondicoesQueValiamNaHoraDaAnalise()
    {
        var contexto = ContextoDeExemplo.Contexto(score: 720, politica: ContextoDeExemplo.Politica(versao: 7));

        var decisao = Registrar(contexto);

        Assert.Equal(contexto.PropostaId, decisao.PropostaId);
        Assert.Equal(720, decisao.ScoreObservado);
        Assert.Equal(0.019m, decisao.TaxaMensalAplicada);
        Assert.Equal(7, decisao.VersaoDaPolitica);
        Assert.Equal(ContextoDeExemplo.Agora, decisao.AvaliadaEm);
    }

    [Fact]
    public void GuardaUmaLinhaPorRegraNaOrdemDoLaudo()
    {
        var decisao = Registrar(ContextoDeExemplo.Contexto());

        Assert.Equal(5, decisao.Avaliacoes.Count);
        Assert.Equal(Enumerable.Range(0, 5), decisao.Avaliacoes.Select(avaliacao => avaliacao.Ordem));
        Assert.Equal(RestricaoCadastral.CodigoDaRegra, decisao.Avaliacoes[0].Codigo);
        Assert.All(decisao.Avaliacoes, avaliacao => Assert.Equal(decisao.Id, avaliacao.DecisaoId));
    }

    [Fact]
    public void SeparaAsReprovacoesParaQuemSoQuerSaberOQueFaltou()
    {
        var decisao = Registrar(ContextoDeExemplo.Contexto(score: 200, rendaMensal: 1_000m));

        Assert.False(decisao.Aprovada);
        Assert.Equal(
            [ScoreMinimo.CodigoDaRegra, ComprometimentoDeRenda.CodigoDaRegra],
            decisao.Reprovacoes.Select(avaliacao => avaliacao.Codigo));
    }

    [Fact]
    public void CopiaOsNumerosDeCadaVeredicto()
    {
        var decisao = Registrar(ContextoDeExemplo.Contexto(score: 480));

        var doScore = decisao.Avaliacoes.Single(
            avaliacao => avaliacao.Codigo == ScoreMinimo.CodigoDaRegra);

        Assert.Equal(480m, doScore.ValorObservado);
        Assert.Equal(500m, doScore.LimiteExigido);
        Assert.False(doScore.Aprovou);

        var daRestricao = decisao.Avaliacoes.Single(
            avaliacao => avaliacao.Codigo == RestricaoCadastral.CodigoDaRegra);

        Assert.Null(daRestricao.ValorObservado);
    }

    public class TransicaoComLaudo
    {
        private static Proposta EmAnalise()
        {
            var proposta = Proposta.Rascunho(DadosDeExemplo.Validos(), DadosDeExemplo.Agora);
            proposta.EnviarParaAnalise(DadosDeExemplo.Agora, "teste");
            return proposta;
        }

        private static Decisao LaudoDe(Proposta proposta, bool aprovando)
        {
            var contexto = ContextoDeExemplo.Contexto(
                score: aprovando ? 820 : 200,
                rendaMensal: aprovando ? 12_000m : 1_000m) with
            {
                PropostaId = proposta.Id,
            };

            return Decisao.Registrar(contexto, Motor.Avaliar(contexto), DadosDeExemplo.Agora);
        }

        [Fact]
        public void AprovarGuardaOLaudoJuntoDaTransicao()
        {
            var proposta = EmAnalise();
            var laudo = LaudoDe(proposta, aprovando: true);

            proposta.Aprovar(laudo, DadosDeExemplo.Agora, "motor:decisao");

            Assert.Equal(EstadoDaProposta.Aprovada, proposta.Estado);
            Assert.Same(laudo, Assert.Single(proposta.Decisoes));
            Assert.Equal(EstadoDaProposta.Aprovada, proposta.Transicoes[^1].Para);
        }

        [Fact]
        public void NegarGuardaOLaudoJuntoDaTransicao()
        {
            var proposta = EmAnalise();
            var laudo = LaudoDe(proposta, aprovando: false);

            proposta.Negar(laudo, DadosDeExemplo.Agora, "motor:decisao");

            Assert.Equal(EstadoDaProposta.Negada, proposta.Estado);
            Assert.Single(proposta.Decisoes);
        }

        // O laudo e a transicao nao podem discordar: aprovar carregando um parecer que
        // reprovou seria o jeito mais silencioso de fraudar a auditoria.
        [Fact]
        public void AprovarComLaudoQueReprovouEhRecusado()
        {
            var proposta = EmAnalise();
            var laudo = LaudoDe(proposta, aprovando: false);

            Assert.Throws<PropostaInvalidaException>(
                () => proposta.Aprovar(laudo, DadosDeExemplo.Agora, "motor:decisao"));

            Assert.Equal(EstadoDaProposta.EmAnalise, proposta.Estado);
            Assert.Empty(proposta.Decisoes);
        }

        [Fact]
        public void NegarComLaudoQueAprovouEhRecusado()
        {
            var proposta = EmAnalise();

            Assert.Throws<PropostaInvalidaException>(
                () => proposta.Negar(LaudoDe(proposta, aprovando: true), DadosDeExemplo.Agora, "motor:decisao"));
        }

        [Fact]
        public void LaudoDeOutraPropostaEhRecusado()
        {
            var proposta = EmAnalise();
            var outra = EmAnalise();

            Assert.Throws<PropostaInvalidaException>(
                () => proposta.Aprovar(LaudoDe(outra, aprovando: true), DadosDeExemplo.Agora, "motor:decisao"));
        }

        // Rascunho nao vai direto para aprovada: a maquina de estados barra antes de o
        // laudo ser sequer olhado.
        [Fact]
        public void NaoDaParaAprovarPropostaQueNemEntrouEmAnalise()
        {
            var proposta = Proposta.Rascunho(DadosDeExemplo.Validos(), DadosDeExemplo.Agora);

            Assert.Throws<TransicaoInvalidaException>(
                () => proposta.Aprovar(LaudoDe(proposta, aprovando: true), DadosDeExemplo.Agora, "motor:decisao"));

            Assert.Empty(proposta.Decisoes);
            Assert.Empty(proposta.Transicoes);
        }
    }
}
