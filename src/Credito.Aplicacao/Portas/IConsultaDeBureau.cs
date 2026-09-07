using Credito.Dominio.Comum;

namespace Credito.Aplicacao.Portas;

public sealed record InformacaoDeBureau(int Score, bool TemRestricao, DateTimeOffset ConsultadaEm);

/// <summary>
/// Consulta a birô de crédito.
/// </summary>
/// <remarks>
/// Recebe o CPF em texto claro de propósito. Birô de verdade precisa do número, e deixar
/// isso na assinatura obriga quem chama a passar por <c>Revelar</c> — que é justamente o
/// ponto que uma revisão de segurança quer encontrar de primeira.
/// </remarks>
public interface IConsultaDeBureau
{
    Task<InformacaoDeBureau> Consultar(Cpf cpf, CancellationToken cancelamento);
}
