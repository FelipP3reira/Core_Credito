using Credito.Dominio.Erros;

namespace Credito.Aplicacao.Erros;

/// <summary>
/// O banco respondeu, e a resposta foi nao.
/// </summary>
/// <remarks>
/// Recusa e resposta valida, e nao falha: saldo insuficiente, conta bloqueada, conta que
/// nao existe. Repetir o pedido nao muda nada — quem precisa agir e uma pessoa, e por isso
/// o motivo do outro lado vem junto em vez de virar "erro ao chamar o banco".
/// </remarks>
public sealed class ContaBancariaRecusouException : DominioException
{
    public ContaBancariaRecusouException(string motivo) : base(motivo)
    {
    }

    public ContaBancariaRecusouException(string motivo, Exception causa) : base(motivo, causa)
    {
    }
}

/// <summary>
/// O banco nao respondeu, ou respondeu algo que nao da para interpretar.
/// </summary>
/// <remarks>
/// Diferente da recusa de proposito: aqui repetir faz sentido, porque o pedido pode ter
/// chegado e a resposta ter se perdido. Como a chave e derivada do contrato, a repeticao e
/// segura — o banco reconhece o que ja fez.
/// </remarks>
public sealed class ContaBancariaIndisponivelException : DominioException
{
    public ContaBancariaIndisponivelException(string mensagem) : base(mensagem)
    {
    }

    public ContaBancariaIndisponivelException(string mensagem, Exception causa) : base(mensagem, causa)
    {
    }
}
