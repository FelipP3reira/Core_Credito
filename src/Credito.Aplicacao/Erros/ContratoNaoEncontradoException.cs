using Credito.Dominio.Erros;

namespace Credito.Aplicacao.Erros;

public sealed class ContratoNaoEncontradoException : DominioException
{
    public ContratoNaoEncontradoException(Guid propostaId)
        : base($"A proposta {propostaId} nao tem contrato.") => PropostaId = propostaId;

    public ContratoNaoEncontradoException(string mensagem) : base(mensagem)
    {
    }

    public ContratoNaoEncontradoException(string mensagem, Exception causa) : base(mensagem, causa)
    {
    }

    public Guid PropostaId { get; }
}
