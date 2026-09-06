namespace Credito.Api.Configuracao;

public sealed class LimiteDeSubmissao
{
    public const string Secao = "LimiteDeSubmissao";

    public int Permitidas { get; set; } = 20;

    public int JanelaEmSegundos { get; set; } = 60;
}
