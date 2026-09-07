namespace Credito.Aplicacao.Erros;

/// <summary>
/// Nao ha politica de credito vigente para analisar.
/// </summary>
/// <remarks>
/// Nao herda de DominioException de proposito. O cliente nao fez nada errado — o sistema
/// e que subiu sem politica cadastrada, e isso precisa aparecer como falha de servidor,
/// com alarme, e nao como 400 que some no meio dos erros de validacao.
/// </remarks>
public sealed class PoliticaVigenteAusenteException : Exception
{
    public PoliticaVigenteAusenteException()
        : base("Nenhuma politica de credito vigente. Rode as migracoes ou cadastre a politica inicial.")
    {
    }

    public PoliticaVigenteAusenteException(string mensagem) : base(mensagem)
    {
    }

    public PoliticaVigenteAusenteException(string mensagem, Exception causa) : base(mensagem, causa)
    {
    }
}
