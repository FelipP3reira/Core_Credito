using Credito.Aplicacao.Erros;
using Credito.Aplicacao.Portas;
using Credito.Dominio.Comum;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Consultas;

/// <param name="Cpf">
/// Opcional, e em texto claro. Chega pelo corpo de um POST, nunca pela URL: caminho e
/// cadeia de consulta aparecem em registro de acesso, historico de navegador e cache de
/// intermediario, e nenhum desses lugares deveria guardar CPF.
/// </param>
public sealed record PedidoDeListagem(
    EstadoDaProposta? Estado = null,
    DateTimeOffset? De = null,
    DateTimeOffset? Ate = null,
    string? Cpf = null,
    string? Cursor = null,
    int? Tamanho = null);

public sealed class ListarPropostas
{
    public const int TamanhoPadrao = 20;
    public const int TamanhoMaximo = 100;

    private readonly IConsultaDePropostas consulta;
    private readonly IProtetorDeCpf protetor;

    public ListarPropostas(IConsultaDePropostas consulta, IProtetorDeCpf protetor)
    {
        this.consulta = consulta;
        this.protetor = protetor;
    }

    public async Task<PaginaDePropostas> Executar(PedidoDeListagem pedido, CancellationToken cancelamento)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var tamanho = TamanhoValido(pedido.Tamanho);

        if (pedido.De > pedido.Ate)
        {
            throw new ConsultaInvalidaException("O inicio do periodo e posterior ao fim.");
        }

        var filtro = new FiltroDeLeitura(
            pedido.Estado,
            pedido.De,
            pedido.Ate,
            HashDoCpf(pedido.Cpf),
            Continuacao(pedido.Cursor),

            // Uma linha a mais do que o cliente pediu: se ela vier, existe pagina
            // seguinte. Um COUNT separado responderia o mesmo varrendo a tabela toda.
            tamanho + 1);

        var linhas = await consulta.Listar(filtro, cancelamento).ConfigureAwait(false);

        return linhas.Count <= tamanho
            ? new PaginaDePropostas(linhas, Proximo: null)
            : new PaginaDePropostas(
                [.. linhas.Take(tamanho)],
                new MarcadorDaPagina(linhas[tamanho - 1].CriadaEm, linhas[tamanho - 1].Id).Codificar());
    }

    private static int TamanhoValido(int? pedido)
    {
        if (pedido is not { } tamanho)
        {
            return TamanhoPadrao;
        }

        // Recusa em vez de aparar em silencio: quem pede mil linhas e recebe cem sem
        // aviso conclui que so existem cem.
        if (tamanho is < 1 || tamanho > TamanhoMaximo)
        {
            throw new ConsultaInvalidaException(
                $"Tamanho de pagina precisa estar entre 1 e {TamanhoMaximo}.");
        }

        return tamanho;
    }

    private static MarcadorDaPagina? Continuacao(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        if (!MarcadorDaPagina.TentarDecodificar(cursor, out var marcador))
        {
            throw new ConsultaInvalidaException("Cursor invalido. Use o valor devolvido na pagina anterior.");
        }

        return marcador;
    }

    /// <remarks>
    /// Passa pelo protetor em vez de calcular o hash aqui: o pepper e o algoritmo moram
    /// num lugar so, e busca que use outra formula simplesmente nao acha nada. A cifra
    /// que sai junto e descartada — o indice e o hash.
    /// </remarks>
    private string? HashDoCpf(string? cpf) =>
        string.IsNullOrWhiteSpace(cpf) ? null : protetor.Proteger(Cpf.Criar(cpf)).Hash;
}
