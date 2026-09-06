namespace Credito.Infraestrutura.Seguranca;

public sealed class OpcoesDeProtecaoDeCpf
{
    public const string Secao = "ProtecaoDeCpf";

    /// <summary>Segredo do HMAC que gera o hash de busca. Vive fora do banco, sempre.</summary>
    public string Pepper { get; set; } = string.Empty;

    /// <summary>Chave AES de 256 bits em base64.</summary>
    public string ChaveDeCifra { get; set; } = string.Empty;
}
