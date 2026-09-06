using Credito.Dominio.Comum;
using Credito.Dominio.Erros;

namespace Credito.Testes.Unidade.Comum;

public class CpfTestes
{
    [Theory]
    [InlineData("52998224725", "52998224725")]
    [InlineData("529.982.247-25", "52998224725")]
    [InlineData(" 529 982 247 25 ", "52998224725")]
    [InlineData("111.444.777-35", "11144477735")]
    public void AceitaCpfValidoComOuSemFormatacao(string entrada, string esperado)
    {
        Assert.True(Cpf.TentarCriar(entrada, out var cpf));
        Assert.Equal(esperado, cpf.Numero);
    }

    [Theory]
    [InlineData("52998224724")]
    [InlineData("52998224715")]
    public void RejeitaDigitoVerificadorErrado(string entrada) =>
        Assert.False(Cpf.TentarCriar(entrada, out _));

    // Sequencias repetidas passam no calculo do digito verificador. Sem o descarte
    // explicito, 111.111.111-11 entraria como CPF valido.
    [Theory]
    [InlineData("00000000000")]
    [InlineData("11111111111")]
    [InlineData("99999999999")]
    public void RejeitaDigitosTodosIguais(string entrada) =>
        Assert.False(Cpf.TentarCriar(entrada, out _));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("5299822472")]
    [InlineData("529982247250")]
    [InlineData("abcdefghijk")]
    public void RejeitaEntradaMalFormada(string? entrada) =>
        Assert.False(Cpf.TentarCriar(entrada, out _));

    [Fact]
    public void CriarLancaQuandoInvalido() =>
        Assert.Throws<CpfInvalidoException>(() => Cpf.Criar("11111111111"));

    [Fact]
    public void MensagemDeErroNaoRepeteONumeroRecebido()
    {
        var erro = Assert.Throws<CpfInvalidoException>(() => Cpf.Criar("52998224724"));

        Assert.DoesNotContain("52998224724", erro.Message, StringComparison.Ordinal);
    }

    // A protecao mais importante da classe: interpolar um Cpf em log ou mensagem
    // e o vazamento mais facil de cometer, entao o caminho padrao precisa ser o seguro.
    [Fact]
    public void ToStringNaoExpoeONumeroCompleto()
    {
        var cpf = Cpf.Criar("529.982.247-25");

        var texto = $"solicitante {cpf}";

        Assert.DoesNotContain("52998224725", texto, StringComparison.Ordinal);
        Assert.Equal("solicitante ***.***.247-**", texto);
    }

    [Fact]
    public void MascaradoRevelaApenasOTerceiroBloco() =>
        Assert.Equal("***.***.247-**", Cpf.Criar("52998224725").Mascarado);

    [Fact]
    public void NumeroDevolveOsDigitosSemFormatacao() =>
        Assert.Equal("52998224725", Cpf.Criar("529.982.247-25").Numero);

    [Fact]
    public void MesmoCpfEmFormatosDiferentesEhIgual()
    {
        var comPontuacao = Cpf.Criar("529.982.247-25");
        var soDigitos = Cpf.Criar("52998224725");

        Assert.Equal(comPontuacao, soDigitos);
        Assert.Equal(comPontuacao.GetHashCode(), soDigitos.GetHashCode());
    }

    [Fact]
    public void CpfsDiferentesNaoSaoIguais() =>
        Assert.NotEqual(Cpf.Criar("52998224725"), Cpf.Criar("11144477735"));
}
