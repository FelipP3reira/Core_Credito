using Credito.Dominio.Erros;

namespace Credito.Aplicacao.Erros;

/// <summary>
/// Mesma chave de idempotencia, conteudo diferente. Devolver a proposta ja gravada
/// seria pior que falhar: o cliente receberia 200 para dados que nunca entraram.
/// </summary>
public sealed class ChaveDeIdempotenciaReutilizadaException : DominioException
{
    public ChaveDeIdempotenciaReutilizadaException()
        : base("Chave de idempotencia ja usada para um pedido com outro conteudo.")
    {
    }

    public ChaveDeIdempotenciaReutilizadaException(string mensagem) : base(mensagem)
    {
    }

    public ChaveDeIdempotenciaReutilizadaException(string mensagem, Exception causa) : base(mensagem, causa)
    {
    }
}
