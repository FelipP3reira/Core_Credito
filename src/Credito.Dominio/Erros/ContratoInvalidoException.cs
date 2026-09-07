namespace Credito.Dominio.Erros;

public sealed class ContratoInvalidoException : DominioException
{
    public ContratoInvalidoException(string mensagem) : base(mensagem)
    {
    }

    public ContratoInvalidoException(string mensagem, Exception causa) : base(mensagem, causa)
    {
    }
}
