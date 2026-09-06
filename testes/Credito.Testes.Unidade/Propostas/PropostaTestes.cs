using Credito.Dominio.Erros;
using Credito.Dominio.Propostas;

namespace Credito.Testes.Unidade.Propostas;

public class PropostaTestes
{
    [Fact]
    public void NasceEmRascunhoSemHistorico()
    {
        var proposta = Proposta.Rascunho(DadosDeExemplo.Validos(), DadosDeExemplo.Agora);

        Assert.Equal(EstadoDaProposta.Rascunho, proposta.Estado);
        Assert.Empty(proposta.Transicoes);
        Assert.Equal(DadosDeExemplo.Agora, proposta.CriadaEm);
        Assert.Equal(proposta.CriadaEm, proposta.AtualizadaEm);
        Assert.NotEqual(Guid.Empty, proposta.Id);
    }

    [Fact]
    public void RemoveEspacoAoRedorDoNome()
    {
        var proposta = Proposta.Rascunho(
            DadosDeExemplo.Validos(nomeSolicitante: "  Ana Ribeiro  "),
            DadosDeExemplo.Agora);

        Assert.Equal("Ana Ribeiro", proposta.NomeSolicitante);
    }

    [Fact]
    public void EnviarParaAnaliseGravaATransicao()
    {
        var proposta = Proposta.Rascunho(DadosDeExemplo.Validos(), DadosDeExemplo.Agora);
        var depois = DadosDeExemplo.Agora.AddMinutes(3);

        proposta.EnviarParaAnalise(depois, "api:submissao");

        Assert.Equal(EstadoDaProposta.EmAnalise, proposta.Estado);
        Assert.Equal(depois, proposta.AtualizadaEm);

        var transicao = Assert.Single(proposta.Transicoes);
        Assert.Equal(EstadoDaProposta.Rascunho, transicao.De);
        Assert.Equal(EstadoDaProposta.EmAnalise, transicao.Para);
        Assert.Equal(depois, transicao.OcorridaEm);
        Assert.Equal("api:submissao", transicao.Origem);
        Assert.Equal(proposta.Id, transicao.PropostaId);
    }

    // O portao precisa barrar ANTES de mudar qualquer coisa: agregado que lanca
    // no meio da mutacao fica em estado invalido na memoria de quem capturou o erro.
    [Fact]
    public void TransicaoInvalidaNaoDeixaRastroNemMudaOEstado()
    {
        var proposta = DadosDeExemplo.EmAnalise();

        Assert.Throws<TransicaoInvalidaException>(
            () => proposta.EnviarParaAnalise(DadosDeExemplo.Agora, "api:submissao"));

        Assert.Equal(EstadoDaProposta.EmAnalise, proposta.Estado);
        Assert.Single(proposta.Transicoes);
    }

    [Fact]
    public void HistoricoAcumulaNaOrdemDosAcontecimentos()
    {
        var proposta = Proposta.Rascunho(DadosDeExemplo.Validos(), DadosDeExemplo.Agora);

        proposta.EnviarParaAnalise(DadosDeExemplo.Agora.AddMinutes(1), "api:submissao");
        proposta.Cancelar(DadosDeExemplo.Agora.AddMinutes(2), "api:cancelamento");

        Assert.Equal(
            [EstadoDaProposta.EmAnalise, EstadoDaProposta.Cancelada],
            proposta.Transicoes.Select(transicao => transicao.Para));
    }

    [Fact]
    public void CancelarDuasVezesLanca()
    {
        var proposta = Proposta.Rascunho(DadosDeExemplo.Validos(), DadosDeExemplo.Agora);
        proposta.Cancelar(DadosDeExemplo.Agora, "api:cancelamento");

        Assert.Throws<TransicaoInvalidaException>(
            () => proposta.Cancelar(DadosDeExemplo.Agora, "api:cancelamento"));
    }

    [Fact]
    public void RejeitaChaveDeIdempotenciaVazia() =>
        Assert.Throws<PropostaInvalidaException>(
            () => Proposta.Rascunho(DadosDeExemplo.Validos(chaveIdempotencia: "  "), DadosDeExemplo.Agora));

    [Fact]
    public void RejeitaNomeVazio() =>
        Assert.Throws<PropostaInvalidaException>(
            () => Proposta.Rascunho(DadosDeExemplo.Validos(nomeSolicitante: "   "), DadosDeExemplo.Agora));

    [Fact]
    public void RejeitaNomeAcimaDoLimite() =>
        Assert.Throws<PropostaInvalidaException>(
            () => Proposta.Rascunho(
                DadosDeExemplo.Validos(nomeSolicitante: new string('a', Proposta.TamanhoMaximoDoNome + 1)),
                DadosDeExemplo.Agora));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void RejeitaRendaNaoPositiva(decimal rendaMensal) =>
        Assert.Throws<PropostaInvalidaException>(
            () => Proposta.Rascunho(DadosDeExemplo.Validos(rendaMensal: rendaMensal), DadosDeExemplo.Agora));

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public void RejeitaValorSolicitadoNaoPositivo(decimal valorSolicitado) =>
        Assert.Throws<PropostaInvalidaException>(
            () => Proposta.Rascunho(DadosDeExemplo.Validos(valorSolicitado: valorSolicitado), DadosDeExemplo.Agora));

    [Theory]
    [InlineData(0)]
    [InlineData(-12)]
    public void RejeitaPrazoAbaixoDeUmMes(int prazoEmMeses) =>
        Assert.Throws<PropostaInvalidaException>(
            () => Proposta.Rascunho(DadosDeExemplo.Validos(prazoEmMeses: prazoEmMeses), DadosDeExemplo.Agora));

    [Fact]
    public void AceitaQuemCompletaAIdadeMinimaHoje()
    {
        var hoje = DateOnly.FromDateTime(DadosDeExemplo.Agora.UtcDateTime);
        var aniversarioHoje = hoje.AddYears(-Proposta.IdadeMinima);

        var proposta = Proposta.Rascunho(
            DadosDeExemplo.Validos(dataDeNascimento: aniversarioHoje),
            DadosDeExemplo.Agora);

        Assert.Equal(EstadoDaProposta.Rascunho, proposta.Estado);
    }

    [Fact]
    public void RejeitaQuemCompletaAIdadeMinimaAmanha()
    {
        var hoje = DateOnly.FromDateTime(DadosDeExemplo.Agora.UtcDateTime);
        var aniversarioAmanha = hoje.AddDays(1).AddYears(-Proposta.IdadeMinima);

        Assert.Throws<PropostaInvalidaException>(
            () => Proposta.Rascunho(
                DadosDeExemplo.Validos(dataDeNascimento: aniversarioAmanha),
                DadosDeExemplo.Agora));
    }
}
