using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Consultas;

/// <param name="PorEstado">
/// Sempre com todos os estados, inclusive os zerados. Omitir os vazios obrigaria quem le a
/// adivinhar se "nao veio Negada" quer dizer nenhuma negativa ou um estado que o relatorio
/// desconhece — e a segunda leitura e a que passa despercebida.
/// </param>
public sealed record ResumoDaCarteira(
    DateTimeOffset? De,
    DateTimeOffset? Ate,
    int Total,
    decimal ValorSolicitado,
    IReadOnlyList<ContagemPorEstado> PorEstado);

public sealed class ResumirCarteira
{
    private static readonly EstadoDaProposta[] TodosOsEstados = Enum.GetValues<EstadoDaProposta>();

    private readonly IConsultaDePropostas consulta;

    public ResumirCarteira(IConsultaDePropostas consulta) => this.consulta = consulta;

    public async Task<ResumoDaCarteira> Executar(
        DateTimeOffset? de,
        DateTimeOffset? ate,
        CancellationToken cancelamento)
    {
        if (de > ate)
        {
            throw new ConsultaInvalidaException("O inicio do periodo e posterior ao fim.");
        }

        var contagens = await consulta.Resumir(de, ate, cancelamento).ConfigureAwait(false);
        var porEstado = contagens.ToDictionary(linha => linha.Estado);

        var completo = Array.ConvertAll(
            TodosOsEstados,
            estado => porEstado.TryGetValue(estado, out var linha)
                ? linha
                : new ContagemPorEstado(estado, Quantidade: 0, ValorSolicitado: 0m));

        return new ResumoDaCarteira(
            de,
            ate,
            completo.Sum(linha => linha.Quantidade),
            completo.Sum(linha => linha.ValorSolicitado),
            completo);
    }
}
