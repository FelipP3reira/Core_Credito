using System.Security.Cryptography;
using System.Text;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Comum;
using Microsoft.Extensions.Options;

namespace Credito.Infraestrutura.Seguranca;

/// <summary>
/// Cifra o CPF com AES-GCM e gera um hash HMAC-SHA256 separado para busca.
/// </summary>
/// <remarks>
/// Sao dois campos porque cada um resolve um problema que o outro nao resolve. A
/// cifra usa nonce novo a cada chamada, entao o mesmo CPF gera textos diferentes —
/// bom para privacidade, inutil para procurar no banco. O hash e deterministico e
/// vira indice, mas nao volta.
/// <para>
/// O pepper e o que segura o hash de pe: CPF tem cerca de um bilhao de valores
/// validos, entao um dump do banco sem o pepper nao permite enumerar, e um dump
/// COM o pepper permite. Por isso ele nunca mora no banco nem no repositorio.
/// Um KDF lento no lugar do HMAC daria folga mesmo com o pepper vazado, ao custo
/// de tornar lenta toda busca por CPF — troca que nao compensa aqui.
/// </para>
/// </remarks>
public sealed class ProtetorDeCpf : IProtetorDeCpf
{
    private const int TamanhoDaChaveEmBytes = 32;
    private const int TamanhoDoNonce = 12;
    private const int TamanhoDaTag = 16;

    private readonly byte[] chaveDeCifra;
    private readonly byte[] pepper;

    public ProtetorDeCpf(IOptions<OpcoesDeProtecaoDeCpf> opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        var valores = opcoes.Value;

        if (string.IsNullOrWhiteSpace(valores.Pepper))
        {
            throw new InvalidOperationException(
                $"{OpcoesDeProtecaoDeCpf.Secao}:Pepper nao configurado. Veja o .env.example.");
        }

        chaveDeCifra = LerChave(valores.ChaveDeCifra);
        pepper = Encoding.UTF8.GetBytes(valores.Pepper);
    }

    public CpfProtegido Proteger(Cpf cpf)
    {
        ArgumentNullException.ThrowIfNull(cpf);

        var digitos = Encoding.UTF8.GetBytes(cpf.Numero);

        return new CpfProtegido(Convert.ToBase64String(HMACSHA256.HashData(pepper, digitos)), Cifrar(digitos));
    }

    public Cpf Revelar(CpfProtegido protegido)
    {
        ArgumentNullException.ThrowIfNull(protegido);

        return Cpf.Criar(Encoding.UTF8.GetString(Decifrar(protegido.Cifrado)));
    }

    private string Cifrar(byte[] digitos)
    {
        // Layout do campo: nonce | tag | texto cifrado. Guardar tudo junto evita
        // uma segunda coluna e mantem a linha do banco autocontida.
        var pacote = new byte[TamanhoDoNonce + TamanhoDaTag + digitos.Length];
        var nonce = pacote.AsSpan(0, TamanhoDoNonce);
        var tag = pacote.AsSpan(TamanhoDoNonce, TamanhoDaTag);
        var cifrado = pacote.AsSpan(TamanhoDoNonce + TamanhoDaTag);

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(chaveDeCifra, TamanhoDaTag);
        aes.Encrypt(nonce, digitos, cifrado, tag);

        return Convert.ToBase64String(pacote);
    }

    private byte[] Decifrar(string cifrado)
    {
        var pacote = Convert.FromBase64String(cifrado);
        if (pacote.Length <= TamanhoDoNonce + TamanhoDaTag)
        {
            throw new CryptographicException("CPF cifrado com tamanho invalido.");
        }

        var digitos = new byte[pacote.Length - TamanhoDoNonce - TamanhoDaTag];

        using var aes = new AesGcm(chaveDeCifra, TamanhoDaTag);
        aes.Decrypt(
            pacote.AsSpan(0, TamanhoDoNonce),
            pacote.AsSpan(TamanhoDoNonce + TamanhoDaTag),
            pacote.AsSpan(TamanhoDoNonce, TamanhoDaTag),
            digitos);

        return digitos;
    }

    // Falha na subida, nao no primeiro cadastro: chave torta so aparece em producao
    // se ninguem conferir aqui.
    private static byte[] LerChave(string emBase64)
    {
        if (string.IsNullOrWhiteSpace(emBase64))
        {
            throw new InvalidOperationException(
                $"{OpcoesDeProtecaoDeCpf.Secao}:ChaveDeCifra nao configurada. Veja o .env.example.");
        }

        if (!Convert.TryFromBase64String(emBase64, new byte[emBase64.Length], out var tamanho))
        {
            throw new InvalidOperationException(
                $"{OpcoesDeProtecaoDeCpf.Secao}:ChaveDeCifra nao e base64 valido.");
        }

        if (tamanho != TamanhoDaChaveEmBytes)
        {
            throw new InvalidOperationException(
                $"{OpcoesDeProtecaoDeCpf.Secao}:ChaveDeCifra precisa ter {TamanhoDaChaveEmBytes} bytes, tem {tamanho}.");
        }

        return Convert.FromBase64String(emBase64);
    }
}
