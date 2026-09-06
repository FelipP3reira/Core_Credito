namespace Credito.Dominio.Erros;

public sealed class AmortizacaoInvalidaException : DominioException
{
    public AmortizacaoInvalidaException(string mensagem) : base(mensagem)
    {
    }

    public AmortizacaoInvalidaException(string mensagem, Exception causa) : base(mensagem, causa)
    {
    }
}
