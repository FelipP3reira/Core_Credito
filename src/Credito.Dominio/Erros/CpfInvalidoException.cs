namespace Credito.Dominio.Erros;

public sealed class CpfInvalidoException : DominioException
{
    // A mensagem nao repete o valor recebido: ele e o proprio dado sensivel.
    public CpfInvalidoException() : base("CPF invalido.")
    {
    }

    public CpfInvalidoException(string mensagem) : base(mensagem)
    {
    }

    public CpfInvalidoException(string mensagem, Exception causa) : base(mensagem, causa)
    {
    }
}
