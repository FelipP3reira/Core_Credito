using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Credito.Aplicacao.Consultas;

/// <summary>
/// Onde a pagina anterior parou: o instante de criacao e o id da ultima linha devolvida.
/// </summary>
/// <remarks>
/// A paginacao e por marcador, e nao por deslocamento. OFFSET/FETCH manda o banco ler e
/// jogar fora tudo que veio antes — a pagina cinquenta custa cinquenta paginas de leitura —
/// e ainda repete ou pula linhas quando alguem cadastra uma proposta no meio da varredura,
/// porque o deslocamento se refere a uma lista que mudou de tamanho.
/// <para>
/// Os dois campos sao necessarios. So o instante nao serve de corte porque duas propostas
/// podem nascer no mesmo tique e uma delas sumiria da paginacao. E o id sozinho tambem nao
/// serve: no SQL Server a comparacao de <c>uniqueidentifier</c> comeca pelos ultimos bytes,
/// entao o prefixo temporal do GUID v7 — justamente o que o torna ordenado — e o ultimo
/// criterio a ser olhado. Ordenar por id daria uma sequencia estavel, mas nao cronologica.
/// </para>
/// </remarks>
public sealed record MarcadorDaPagina(DateTimeOffset CriadaEm, Guid Id)
{
    private const char Separador = '|';

    /// <summary>
    /// Texto opaco: o cliente devolve o que recebeu, sem interpretar.
    /// </summary>
    /// <remarks>
    /// Base64 na variante de URL porque o marcador viaja na cadeia de consulta, onde
    /// <c>+</c> vira espaco e <c>/</c> confunde quem le o caminho. O formato "O" da data
    /// preserva o deslocamento e os sete digitos de fracao: truncar para milissegundos
    /// faria o corte cair no meio de um empate e repetir linhas na pagina seguinte.
    /// </remarks>
    public string Codificar() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{CriadaEm:O}{Separador}{Id:D}")));

    public static bool TentarDecodificar(string? texto, out MarcadorDaPagina marcador)
    {
        marcador = null!;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        string decodificado;
        try
        {
            decodificado = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(texto));
        }
        catch (FormatException)
        {
            // Marcador vem de fora: texto que nao e base64 e pedido invalido, nao falha.
            return false;
        }

        var separador = decodificado.IndexOf(Separador, StringComparison.Ordinal);
        if (separador < 0)
        {
            return false;
        }

        if (!DateTimeOffset.TryParseExact(
                decodificado[..separador],
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var criadaEm))
        {
            return false;
        }

        if (!Guid.TryParseExact(decodificado[(separador + 1)..], "D", out var id))
        {
            return false;
        }

        marcador = new MarcadorDaPagina(criadaEm, id);
        return true;
    }
}
