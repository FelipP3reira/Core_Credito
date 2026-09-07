namespace Credito.Dominio.Erros;

public sealed class PoliticaInvalidaException : DominioException
{
    public PoliticaInvalidaException(string mensagem) : base(mensagem)
    {
    }

    public PoliticaInvalidaException(string mensagem, Exception causa) : base(mensagem, causa)
    {
    }
}
