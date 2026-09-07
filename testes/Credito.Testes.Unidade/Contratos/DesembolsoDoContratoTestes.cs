using Credito.Dominio.Amortizacao;
using Credito.Dominio.Contratos;
using Credito.Dominio.Erros;

namespace Credito.Testes.Unidade.Contratos;

public class DesembolsoDoContratoTestes
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private static Contrato Assinar(Guid? contaId) =>
        Contrato.Assinar(
            Guid.CreateVersion7(),
            new TabelaPrice().Gerar(10_000m, 0.019m, 12),
            DateOnly.FromDateTime(Agora.UtcDateTime).AddDays(30),
            Agora,
            contaId);

    [Fact]
    public void ContratoNasceNaoDesembolsado()
    {
        var contrato = Assinar(Guid.CreateVersion7());

        Assert.False(contrato.EstaDesembolsado);
        Assert.Null(contrato.DesembolsadoEm);
        Assert.Null(contrato.LancamentoDoDesembolsoId);
    }

    [Fact]
    public void DesembolsarGuardaAChaveEOLancamento()
    {
        var contrato = Assinar(Guid.CreateVersion7());
        var lancamento = Guid.CreateVersion7();

        contrato.Desembolsar(contrato.ChaveDeDesembolso(), lancamento, Agora);

        Assert.True(contrato.EstaDesembolsado);
        Assert.Equal(Agora, contrato.DesembolsadoEm);
        Assert.Equal(lancamento, contrato.LancamentoDoDesembolsoId);
        Assert.Equal(contrato.ChaveDeDesembolso(), contrato.ChaveDoDesembolso);
    }

    /// <summary>
    /// A chave e a mesma toda vez que se pergunta. Se ela mudasse a cada chamada, a segunda
    /// tentativa de desembolso chegaria ao banco como um pedido novo.
    /// </summary>
    [Fact]
    public void AChaveDeDesembolsoNaoMuda()
    {
        var contrato = Assinar(Guid.CreateVersion7());

        Assert.Equal(contrato.ChaveDeDesembolso(), contrato.ChaveDeDesembolso());
        Assert.Contains(contrato.Id.ToString("N"), contrato.ChaveDeDesembolso(), StringComparison.Ordinal);
    }

    // Contratos diferentes nao podem compartilhar chave: seria um desembolsar por outro.
    [Fact]
    public void ContratosDiferentesTemChavesDiferentes() =>
        Assert.NotEqual(
            Assinar(Guid.CreateVersion7()).ChaveDeDesembolso(),
            Assinar(Guid.CreateVersion7()).ChaveDeDesembolso());

    [Fact]
    public void AChaveDaParcelaVariaPorNumero()
    {
        var contrato = Assinar(Guid.CreateVersion7());

        Assert.NotEqual(contrato.ChaveDaParcela(1), contrato.ChaveDaParcela(2));
        Assert.Equal(contrato.ChaveDaParcela(3), contrato.ChaveDaParcela(3));
    }

    // A chave da parcela nao pode colidir com a do desembolso: sao movimentos opostos.
    [Fact]
    public void AChaveDaParcelaNaoSeConfundeComADoDesembolso()
    {
        var contrato = Assinar(Guid.CreateVersion7());

        Assert.NotEqual(contrato.ChaveDeDesembolso(), contrato.ChaveDaParcela(1));
    }

    /// <summary>
    /// Reenvio do mesmo desembolso nao muda nada — e o que deixa a operacao ser repetida
    /// ate a resposta chegar.
    /// </summary>
    [Fact]
    public void DesembolsarDeNovoComAMesmaChaveNaoMudaNada()
    {
        var contrato = Assinar(Guid.CreateVersion7());
        var lancamento = Guid.CreateVersion7();
        contrato.Desembolsar(contrato.ChaveDeDesembolso(), lancamento, Agora);

        contrato.Desembolsar(contrato.ChaveDeDesembolso(), Guid.CreateVersion7(), Agora.AddDays(1));

        Assert.Equal(Agora, contrato.DesembolsadoEm);
        Assert.Equal(lancamento, contrato.LancamentoDoDesembolsoId);
    }

    // Chave diferente e uma segunda tentativa de desembolsar o mesmo contrato.
    [Fact]
    public void DesembolsarComOutraChaveEhRecusado()
    {
        var contrato = Assinar(Guid.CreateVersion7());
        contrato.Desembolsar(contrato.ChaveDeDesembolso(), Guid.CreateVersion7(), Agora);

        var erro = Assert.Throws<ContratoInvalidoException>(
            () => contrato.Desembolsar("outra-chave", Guid.CreateVersion7(), Agora));

        Assert.Contains("ja foi desembolsado", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContratoSemContaNaoDesembolsa()
    {
        var contrato = Assinar(contaId: null);

        var erro = Assert.Throws<ContratoInvalidoException>(
            () => contrato.Desembolsar(contrato.ChaveDeDesembolso(), Guid.CreateVersion7(), Agora));

        Assert.Contains("conta", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(contrato.EstaDesembolsado);
    }

    [Fact]
    public void ParcelaInexistenteEhRecusadaAntesDeQualquerCobranca() =>
        Assert.Throws<ContratoInvalidoException>(() => Assinar(Guid.CreateVersion7()).Parcela(99));

    [Fact]
    public void ParcelaExistenteVemComOValorDaCobranca()
    {
        var contrato = Assinar(Guid.CreateVersion7());

        Assert.Equal(contrato.Parcelas[0].Valor, contrato.Parcela(1).Valor);
    }
}
