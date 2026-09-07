using Credito.Dominio.Amortizacao;
using Credito.Dominio.Contratos;
using Credito.Dominio.Erros;

namespace Credito.Testes.Unidade.Contratos;

public class ContratoTestes
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid PropostaId = Guid.CreateVersion7();

    private static Contrato Assinar(
        decimal valorFinanciado = 10_000m,
        decimal taxaMensal = 0.019m,
        int prazoEmMeses = 12,
        DateOnly? primeiroVencimento = null,
        DateTimeOffset? assinadoEm = null,
        SistemaDeAmortizacao sistema = SistemaDeAmortizacao.Price)
    {
        var quando = assinadoEm ?? Agora;
        var cronograma = sistema == SistemaDeAmortizacao.Price
            ? new TabelaPrice().Gerar(valorFinanciado, taxaMensal, prazoEmMeses)
            : new TabelaSac().Gerar(valorFinanciado, taxaMensal, prazoEmMeses);

        return Contrato.Assinar(
            PropostaId,
            cronograma,
            primeiroVencimento ?? DateOnly.FromDateTime(quando.UtcDateTime).AddDays(30),
            quando);
    }

    [Fact]
    public void CongelaOCronogramaParcelaAParcela()
    {
        var contrato = Assinar();

        Assert.Equal(12, contrato.Parcelas.Count);
        Assert.Equal(10_000m, contrato.ValorFinanciado);
        Assert.Equal(0.019m, contrato.TaxaMensal);
        Assert.Equal(SistemaDeAmortizacao.Price, contrato.Sistema);
        Assert.Equal(939.80m, contrato.Parcelas[0].Valor);
        Assert.Equal(10_000m, contrato.Parcelas.Sum(parcela => parcela.Amortizacao));
    }

    [Fact]
    public void VenceUmaParcelaPorMesAPartirDoPrimeiroVencimento()
    {
        var primeiro = new DateOnly(2026, 10, 7);

        var contrato = Assinar(prazoEmMeses: 3, primeiroVencimento: primeiro);

        Assert.Equal(
            [new DateOnly(2026, 10, 7), new DateOnly(2026, 11, 7), new DateOnly(2026, 12, 7)],
            contrato.Parcelas.Select(parcela => parcela.Vencimento));
    }

    /// <summary>
    /// Vencimento em 31 encurta para o ultimo dia do mes que nao tem 31, e volta ao dia 31
    /// nos meses que tem. Somar trinta dias corridos escorregaria a data a cada mes.
    /// </summary>
    [Fact]
    public void VencimentoNoFimDoMesEncurtaEmVezDeEscorregar()
    {
        var contrato = Assinar(
            prazoEmMeses: 4,
            primeiroVencimento: new DateOnly(2026, 10, 31),
            assinadoEm: new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(
            [
                new DateOnly(2026, 10, 31),
                new DateOnly(2026, 11, 30),
                new DateOnly(2026, 12, 31),
                new DateOnly(2027, 1, 31),
            ],
            contrato.Parcelas.Select(parcela => parcela.Vencimento));
    }

    [Fact]
    public void RecusaPrimeiroVencimentoNoPassadoOuNoMesmoDia()
    {
        var hoje = DateOnly.FromDateTime(Agora.UtcDateTime);

        Assert.Throws<ContratoInvalidoException>(() => Assinar(primeiroVencimento: hoje));
        Assert.Throws<ContratoInvalidoException>(() => Assinar(primeiroVencimento: hoje.AddDays(-1)));
    }

    [Fact]
    public void RecusaPrimeiroVencimentoLongeDemais()
    {
        var hoje = DateOnly.FromDateTime(Agora.UtcDateTime);

        Assert.Throws<ContratoInvalidoException>(
            () => Assinar(primeiroVencimento: hoje.AddDays(Contrato.MaximoDeDiasAteOPrimeiroVencimento + 1)));

        // O limite em si continua valendo.
        Assert.Equal(
            12,
            Assinar(primeiroVencimento: hoje.AddDays(Contrato.MaximoDeDiasAteOPrimeiroVencimento))
                .Parcelas.Count);
    }

    [Fact]
    public void ContratoNasceTodoEmAberto()
    {
        var contrato = Assinar();

        Assert.False(contrato.EstaQuitado);
        Assert.Equal(0m, contrato.TotalPago);
        Assert.Equal(contrato.Parcelas.Sum(parcela => parcela.Valor), contrato.SaldoAberto);
        Assert.All(contrato.Parcelas, parcela => Assert.False(parcela.EstaPaga));
    }

    [Fact]
    public void PagarMarcaAParcelaEMoveOSaldo()
    {
        var contrato = Assinar();
        var aberto = contrato.SaldoAberto;

        var parcela = contrato.Pagar(1, Agora, "chave-1");

        Assert.True(parcela.EstaPaga);
        Assert.Equal(Agora, parcela.PagaEm);
        Assert.Equal(parcela.Valor, contrato.TotalPago);
        Assert.Equal(aberto - parcela.Valor, contrato.SaldoAberto);
        Assert.False(contrato.EstaQuitado);
    }

    [Fact]
    public void QuitaQuandoTodasAsParcelasForamPagas()
    {
        var contrato = Assinar(prazoEmMeses: 6);

        foreach (var numero in Enumerable.Range(1, 6))
        {
            contrato.Pagar(numero, Agora, $"chave-{numero}");
        }

        Assert.True(contrato.EstaQuitado);
        Assert.Equal(0m, contrato.SaldoAberto);
        Assert.Equal(contrato.Parcelas.Sum(parcela => parcela.Valor), contrato.TotalPago);
    }

    // Cliente que perdeu a resposta por tempo esgotado reenvia a mesma cobranca. Isso
    // precisa ser reconhecido como repeticao, e nao como segunda tentativa de pagar.
    [Fact]
    public void ReenvioDaMesmaCobrancaNaoMudaNada()
    {
        var contrato = Assinar();

        var primeira = contrato.Pagar(3, Agora, "chave-unica");
        var segunda = contrato.Pagar(3, Agora.AddHours(1), "chave-unica");

        Assert.Same(primeira, segunda);
        Assert.Equal(Agora, segunda.PagaEm);
        Assert.Equal(primeira.Valor, contrato.TotalPago);
    }

    [Fact]
    public void OutraCobrancaSobreParcelaJaPagaEhRecusada()
    {
        var contrato = Assinar();
        contrato.Pagar(3, Agora, "chave-a");

        var erro = Assert.Throws<ContratoInvalidoException>(
            () => contrato.Pagar(3, Agora, "chave-b"));

        Assert.Contains("ja foi paga", erro.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void RecusaParcelaQueNaoExiste(int numero) =>
        Assert.Throws<ContratoInvalidoException>(() => Assinar().Pagar(numero, Agora, "chave"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RecusaPagamentoSemChave(string chave) =>
        Assert.Throws<ContratoInvalidoException>(() => Assinar().Pagar(1, Agora, chave));

    // Pagar fora de ordem e comum: quem antecipa a ultima parcela nao deveria ser barrado.
    [Fact]
    public void AceitaPagamentoForaDeOrdem()
    {
        var contrato = Assinar(prazoEmMeses: 6);

        contrato.Pagar(6, Agora, "chave-6");
        contrato.Pagar(2, Agora, "chave-2");

        Assert.True(contrato.Parcelas[5].EstaPaga);
        Assert.True(contrato.Parcelas[1].EstaPaga);
        Assert.False(contrato.EstaQuitado);
    }

    [Fact]
    public void OSacTambemCongelaComParcelaDecrescente()
    {
        var contrato = Assinar(valorFinanciado: 12_000m, prazoEmMeses: 12, sistema: SistemaDeAmortizacao.Sac);

        Assert.Equal(SistemaDeAmortizacao.Sac, contrato.Sistema);
        Assert.True(contrato.Parcelas[^1].Valor < contrato.Parcelas[0].Valor);
        Assert.Equal(12_000m, contrato.Parcelas.Sum(parcela => parcela.Amortizacao));
    }
}
