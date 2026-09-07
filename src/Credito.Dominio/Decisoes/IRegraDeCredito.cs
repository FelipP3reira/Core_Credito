namespace Credito.Dominio.Decisoes;

/// <summary>
/// Uma condicao de concessao, avaliavel sozinha.
/// </summary>
/// <remarks>
/// Cada regra e uma classe propria em vez de um ramo de um bloco de ifs porque o teste
/// precisa alcancar cada uma isoladamente, e porque regra nova entra sem tocar nas que
/// ja existem.
/// </remarks>
public interface IRegraDeCredito
{
    string Codigo { get; }

    VeredictoDaRegra Avaliar(ContextoDaAnalise contexto);
}
