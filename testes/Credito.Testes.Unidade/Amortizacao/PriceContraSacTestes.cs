using Credito.Dominio.Amortizacao;
using Credito.Dominio.Erros;

namespace Credito.Testes.Unidade.Amortizacao;

/// <summary>
/// O que separa os dois sistemas, com a mesma entrada nos dois lados. Sao as diferencas
/// que o cliente sente e que justificam deixar a escolha configuravel.
/// </summary>
public class PriceContraSacTestes
{
    private const decimal Valor = 60_000m;
    private const decimal Taxa = 0.015m;
    private const int Prazo = 48;

    private static readonly SistemasDeAmortizacao Sistemas = new([new TabelaPrice(), new TabelaSac()]);

    private static Cronograma Price => Sistemas.De(SistemaDeAmortizacao.Price).Gerar(Valor, Taxa, Prazo);

    private static Cronograma Sac => Sistemas.De(SistemaDeAmortizacao.Sac).Gerar(Valor, Taxa, Prazo);

    // O SAC abate mais cedo, entao o saldo que rende juros e menor o contrato inteiro.
    [Fact]
    public void SacPagaMenosJurosNoTotal() => Assert.True(Sac.TotalDeJuros < Price.TotalDeJuros);

    // Em troca, aperta mais no comeco — e e por isso que a escolha entre os dois nao e
    // so questao de custo total.
    [Fact]
    public void SacComecaMaisCaroETerminaMaisBarato()
    {
        Assert.True(Sac.PrimeiraParcela > Price.PrimeiraParcela);
        Assert.True(Sac.UltimaParcela < Price.UltimaParcela);
    }

    [Fact]
    public void OsDoisQuitamOMesmoFinanciado()
    {
        Assert.Equal(Valor, Price.Parcelas.Sum(parcela => parcela.Amortizacao));
        Assert.Equal(Valor, Sac.Parcelas.Sum(parcela => parcela.Amortizacao));
    }

    [Fact]
    public void SemJurosOsDoisDaoNoMesmo() =>
        Assert.Equal(
            Sistemas.De(SistemaDeAmortizacao.Price).Gerar(Valor, 0m, Prazo).TotalPago,
            Sistemas.De(SistemaDeAmortizacao.Sac).Gerar(Valor, 0m, Prazo).TotalPago);

    [Fact]
    public void CadaSistemaSeApresentaComOProprioNome()
    {
        Assert.Equal(SistemaDeAmortizacao.Price, Sistemas.De(SistemaDeAmortizacao.Price).Sistema);
        Assert.Equal(SistemaDeAmortizacao.Sac, Sistemas.De(SistemaDeAmortizacao.Sac).Sistema);
    }

    // Sistema novo no enum sem implementacao correspondente falha na hora de calcular,
    // e nao devolve cronograma vazio.
    [Fact]
    public void SistemaSemImplementacaoFalhaExplicitamente()
    {
        var soPrice = new SistemasDeAmortizacao([new TabelaPrice()]);

        Assert.Throws<AmortizacaoInvalidaException>(() => soPrice.De(SistemaDeAmortizacao.Sac));
    }
}
