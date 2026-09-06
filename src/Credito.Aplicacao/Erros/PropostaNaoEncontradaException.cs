using Credito.Dominio.Erros;

namespace Credito.Aplicacao.Erros;

public sealed class PropostaNaoEncontradaException : DominioException
{
    public PropostaNaoEncontradaException(Guid id) : base($"Proposta {id} nao encontrada.") => Id = id;

    public PropostaNaoEncontradaException(string mensagem) : base(mensagem)
    {
    }

    public PropostaNaoEncontradaException(string mensagem, Exception causa) : base(mensagem, causa)
    {
    }

    public Guid Id { get; }
}
