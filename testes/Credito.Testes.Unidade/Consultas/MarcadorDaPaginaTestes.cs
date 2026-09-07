using Credito.Aplicacao.Consultas;

namespace Credito.Testes.Unidade.Consultas;

public class MarcadorDaPaginaTestes
{
    private static readonly Guid Id = Guid.Parse("0198f3a0-1c2d-7e4f-8a9b-0c1d2e3f4a5b");

    [Fact]
    public void VoltaExatamenteOQueFoiCodificado()
    {
        var original = new MarcadorDaPagina(
            new DateTimeOffset(2026, 9, 7, 12, 34, 56, 789, TimeSpan.FromHours(-3)).AddTicks(1234),
            Id);

        Assert.True(MarcadorDaPagina.TentarDecodificar(original.Codificar(), out var lido));
        Assert.Equal(original.CriadaEm, lido.CriadaEm);
        Assert.Equal(original.CriadaEm.Offset, lido.CriadaEm.Offset);
        Assert.Equal(original.Id, lido.Id);
    }

    // O marcador viaja na cadeia de consulta: caractere que precise de escape ali
    // voltaria diferente do que saiu.
    [Fact]
    public void NaoUsaCaractereQuePrecisaDeEscapeNaUrl()
    {
        var codificado = new MarcadorDaPagina(DateTimeOffset.UtcNow, Id).Codificar();

        Assert.DoesNotContain("+", codificado, StringComparison.Ordinal);
        Assert.DoesNotContain("/", codificado, StringComparison.Ordinal);
        Assert.DoesNotContain("=", codificado, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nao-e-base64!!")]
    [InlineData("c2Vt-c2VwYXJhZG9y")]
    public void EntradaEstragadaEhRecusadaSemEstourar(string? texto) =>
        Assert.False(MarcadorDaPagina.TentarDecodificar(texto, out _));

    [Fact]
    public void RecusaMarcadorComDataQueNaoEhData() =>
        Assert.False(MarcadorDaPagina.TentarDecodificar(
            System.Buffers.Text.Base64Url.EncodeToString(
                System.Text.Encoding.UTF8.GetBytes($"ontem|{Id:D}")),
            out _));

    [Fact]
    public void RecusaMarcadorComIdQueNaoEhGuid() =>
        Assert.False(MarcadorDaPagina.TentarDecodificar(
            System.Buffers.Text.Base64Url.EncodeToString(
                System.Text.Encoding.UTF8.GetBytes("2026-09-07T12:00:00.0000000+00:00|nada")),
            out _));
}
