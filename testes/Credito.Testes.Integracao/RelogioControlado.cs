namespace Credito.Testes.Integracao;

/// <summary>
/// Relógio real com um deslocamento que o teste controla.
/// </summary>
/// <remarks>
/// Parte da hora de verdade, e não de uma data fixa, para que as validações que comparam
/// com o presente — idade mínima do solicitante, janela do primeiro vencimento — continuem
/// se comportando como em produção. O que o teste ajusta é só quanto tempo passou.
/// </remarks>
public sealed class RelogioControlado : TimeProvider
{
    private TimeSpan deslocamento = TimeSpan.Zero;

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + deslocamento;

    public void Avancar(TimeSpan quanto) => deslocamento += quanto;

    public void Reiniciar() => deslocamento = TimeSpan.Zero;
}
