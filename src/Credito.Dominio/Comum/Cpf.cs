using Credito.Dominio.Erros;

namespace Credito.Dominio.Comum;

/// <summary>
/// CPF validado. Nao existe instancia com numero invalido: o construtor e privado
/// e as duas fabricas conferem os digitos verificadores antes de criar.
/// </summary>
/// <remarks>
/// <see cref="ToString"/> devolve o numero MASCARADO de proposito. Interpolar um Cpf
/// num log ou numa mensagem de erro e o jeito mais comum de vazar o dado sem querer,
/// entao o caminho padrao e o seguro e quem precisa do numero real chama
/// <see cref="Numero"/> explicitamente.
/// </remarks>
public sealed class Cpf : IEquatable<Cpf>
{
    private const int QuantidadeDeDigitos = 11;

    private readonly string digitos;

    private Cpf(string digitos) => this.digitos = digitos;

    public string Numero => digitos;

    public string Mascarado => $"***.***.{digitos.Substring(6, 3)}-**";

    public static Cpf Criar(string? entrada)
    {
        if (!TentarCriar(entrada, out var cpf))
        {
            throw new CpfInvalidoException();
        }

        return cpf;
    }

    public static bool TentarCriar(string? entrada, out Cpf cpf)
    {
        cpf = null!;

        if (string.IsNullOrWhiteSpace(entrada))
        {
            return false;
        }

        var digitos = SomenteDigitos(entrada);
        if (digitos.Length != QuantidadeDeDigitos || TodosIguais(digitos))
        {
            return false;
        }

        if (DigitoVerificador(digitos, 9) != digitos[9] || DigitoVerificador(digitos, 10) != digitos[10])
        {
            return false;
        }

        cpf = new Cpf(digitos);
        return true;
    }

    private static string SomenteDigitos(string entrada)
    {
        Span<char> limpo = stackalloc char[entrada.Length];
        var tamanho = 0;

        foreach (var caractere in entrada)
        {
            if (char.IsAsciiDigit(caractere))
            {
                limpo[tamanho++] = caractere;
            }
        }

        return new string(limpo[..tamanho]);
    }

    // Sequencias como 111.111.111-11 passam no calculo do digito verificador,
    // mas nao sao CPF de ninguem. A Receita trata todas como invalidas.
    private static bool TodosIguais(string digitos)
    {
        for (var posicao = 1; posicao < digitos.Length; posicao++)
        {
            if (digitos[posicao] != digitos[0])
            {
                return false;
            }
        }

        return true;
    }

    private static char DigitoVerificador(string digitos, int ateAPosicao)
    {
        var soma = 0;
        var peso = ateAPosicao + 1;

        for (var posicao = 0; posicao < ateAPosicao; posicao++)
        {
            soma += (digitos[posicao] - '0') * peso--;
        }

        var resto = soma % 11;
        return resto < 2 ? '0' : (char)('0' + (11 - resto));
    }

    public bool Equals(Cpf? outro) => outro is not null && digitos == outro.digitos;

    public override bool Equals(object? obj) => Equals(obj as Cpf);

    public override int GetHashCode() => digitos.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Mascarado;
}
