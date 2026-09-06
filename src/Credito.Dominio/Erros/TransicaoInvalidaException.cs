using Credito.Dominio.Propostas;

namespace Credito.Dominio.Erros;

public sealed class TransicaoInvalidaException : DominioException
{
    public TransicaoInvalidaException(EstadoDaProposta de, EstadoDaProposta para)
        : base($"Transicao invalida: {de} para {para}.")
    {
        De = de;
        Para = para;
    }

    public TransicaoInvalidaException(string mensagem) : base(mensagem)
    {
    }

    public TransicaoInvalidaException(string mensagem, Exception causa) : base(mensagem, causa)
    {
    }

    public EstadoDaProposta? De { get; }

    public EstadoDaProposta? Para { get; }
}
