using System.Security.Cryptography;
using Credito.Dominio.Comum;
using Credito.Infraestrutura.Seguranca;
using Microsoft.Extensions.Options;

namespace Credito.Testes.Unidade.Seguranca;

public class ProtetorDeCpfTestes
{
    private const string CpfDeTeste = "529.982.247-25";

    private static readonly string ChaveValida = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string OutraChaveValida = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static ProtetorDeCpf Protetor(string? pepper = null, string? chave = null) =>
        new(Options.Create(new OpcoesDeProtecaoDeCpf
        {
            Pepper = pepper ?? "pepper-de-teste",
            ChaveDeCifra = chave ?? ChaveValida,
        }));

    [Fact]
    public void ProtegerEDepoisRevelarDevolveOMesmoCpf()
    {
        var protetor = Protetor();
        var original = Cpf.Criar(CpfDeTeste);

        Assert.Equal(original, protetor.Revelar(protetor.Proteger(original)));
    }

    [Fact]
    public void HashEhDeterministicoParaOMesmoCpf()
    {
        var protetor = Protetor();

        Assert.Equal(
            protetor.Proteger(Cpf.Criar(CpfDeTeste)).Hash,
            protetor.Proteger(Cpf.Criar("52998224725")).Hash);
    }

    [Fact]
    public void HashDifereEntreCpfsDiferentes()
    {
        var protetor = Protetor();

        Assert.NotEqual(
            protetor.Proteger(Cpf.Criar(CpfDeTeste)).Hash,
            protetor.Proteger(Cpf.Criar("111.444.777-35")).Hash);
    }

    // Se o pepper nao entrasse na conta, um dump do banco seria enumeravel: sao
    // cerca de um bilhao de CPFs validos, um fim de semana de GPU.
    [Fact]
    public void PepperDiferenteProduzHashDiferente()
    {
        var cpf = Cpf.Criar(CpfDeTeste);

        Assert.NotEqual(
            Protetor(pepper: "um-pepper").Proteger(cpf).Hash,
            Protetor(pepper: "outro-pepper").Proteger(cpf).Hash);
    }

    [Fact]
    public void CifraNaoSeRepeteEntreDuasChamadas()
    {
        var protetor = Protetor();
        var cpf = Cpf.Criar(CpfDeTeste);

        Assert.NotEqual(protetor.Proteger(cpf).Cifrado, protetor.Proteger(cpf).Cifrado);
    }

    [Fact]
    public void CifraNaoCarregaOsDigitosEmTextoClaro()
    {
        var cifrado = Protetor().Proteger(Cpf.Criar(CpfDeTeste)).Cifrado;

        Assert.DoesNotContain("52998224725", cifrado, StringComparison.Ordinal);
    }

    [Fact]
    public void OutraChaveNaoDecifra()
    {
        var protegido = Protetor().Proteger(Cpf.Criar(CpfDeTeste));

        Assert.Throws<AuthenticationTagMismatchException>(
            () => Protetor(chave: OutraChaveValida).Revelar(protegido));
    }

    // A tag do GCM e o que separa cifra de cifra autenticada: sem ela, mexer no
    // texto cifrado devolveria lixo silenciosamente em vez de estourar.
    [Fact]
    public void AdulterarOTextoCifradoEhDetectado()
    {
        var protetor = Protetor();
        var pacote = Convert.FromBase64String(protetor.Proteger(Cpf.Criar(CpfDeTeste)).Cifrado);

        pacote[^1] ^= 0xFF;
        var adulterado = new CpfProtegido("qualquer-hash", Convert.ToBase64String(pacote));

        Assert.Throws<AuthenticationTagMismatchException>(() => protetor.Revelar(adulterado));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RecusaSubirSemPepper(string pepper) =>
        Assert.Throws<InvalidOperationException>(() => Protetor(pepper: pepper));

    [Fact]
    public void RecusaSubirSemChave() =>
        Assert.Throws<InvalidOperationException>(() => Protetor(chave: ""));

    [Fact]
    public void RecusaChaveQueNaoEhBase64() =>
        Assert.Throws<InvalidOperationException>(() => Protetor(chave: "nao#eh$base64!"));

    // Chave de 128 bits e valida como base64 e passaria batido ate a primeira
    // cifragem em producao.
    [Fact]
    public void RecusaChaveComTamanhoErrado()
    {
        var chaveCurta = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        var erro = Assert.Throws<InvalidOperationException>(() => Protetor(chave: chaveCurta));

        Assert.Contains("32 bytes", erro.Message, StringComparison.Ordinal);
    }
}
