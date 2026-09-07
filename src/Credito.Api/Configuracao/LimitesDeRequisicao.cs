namespace Credito.Api.Configuracao;

/// <summary>Quantas requisicoes um mesmo IP pode fazer dentro de uma janela.</summary>
public abstract class LimiteDeRequisicoes
{
    public int Permitidas { get; set; }

    public int JanelaEmSegundos { get; set; } = 60;
}

public sealed class LimiteDeSubmissao : LimiteDeRequisicoes
{
    public const string Secao = "LimiteDeSubmissao";

    public LimiteDeSubmissao() => Permitidas = 20;
}

/// <summary>
/// Limite proprio, e mais apertado, para a busca por CPF.
/// </summary>
/// <remarks>
/// O hash do CPF e deterministico, entao quem tem acesso a rota consegue perguntar
/// "existe proposta para este numero?" — e CPF tem cerca de um bilhao de valores validos,
/// o que faz da rota um enumerador se ninguem contar as perguntas. Compartilhar o limite
/// da submissao nao serviria: cadastro e busca tem custos e frequencias legitimas
/// diferentes, e um numero que sirva para os dois vai estar errado para um deles.
/// </remarks>
public sealed class LimiteDeBusca : LimiteDeRequisicoes
{
    public const string Secao = "LimiteDeBusca";

    public LimiteDeBusca() => Permitidas = 10;
}
