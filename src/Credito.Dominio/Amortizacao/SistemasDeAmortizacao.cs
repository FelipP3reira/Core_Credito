using Credito.Dominio.Erros;

namespace Credito.Dominio.Amortizacao;

/// <summary>
/// Resolve o sistema escolhido na proposta para quem sabe calcular.
/// </summary>
public sealed class SistemasDeAmortizacao
{
    private readonly Dictionary<SistemaDeAmortizacao, ISistemaDeAmortizacao> porSistema;

    public SistemasDeAmortizacao(IEnumerable<ISistemaDeAmortizacao> sistemas)
    {
        ArgumentNullException.ThrowIfNull(sistemas);

        porSistema = sistemas.ToDictionary(sistema => sistema.Sistema);
    }

    public ISistemaDeAmortizacao De(SistemaDeAmortizacao escolhido) =>
        porSistema.TryGetValue(escolhido, out var sistema)
            ? sistema
            : throw new AmortizacaoInvalidaException($"Sistema de amortizacao {escolhido} nao implementado.");
}
