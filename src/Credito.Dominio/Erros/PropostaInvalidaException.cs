namespace Credito.Dominio.Erros;

public sealed class PropostaInvalidaException : DominioException
{
    public PropostaInvalidaException(string mensagem) : base(mensagem)
    {
    }

    public PropostaInvalidaException(string mensagem, Exception causa) : base(mensagem, causa)
    {
    }
}
