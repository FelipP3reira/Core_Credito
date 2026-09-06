using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Credito.Aplicacao.Propostas;

/// <summary>
/// Resumo do conteudo de um cadastro, usado para separar reenvio do mesmo formulario
/// de cliente reaproveitando a chave de idempotencia com outros dados.
/// </summary>
internal static class ImpressaoDoPedido
{
    // Separador que nao aparece em nenhum dos campos: sem ele, mudar onde um campo
    // termina e o outro comeca produziria a mesma impressao para pedidos diferentes.
    private const char Separador = '\u001f';

    public static string Calcular(CadastroDeProposta cadastro)
    {
        var campos = string.Join(
            Separador,
            SomenteDigitos(cadastro.Cpf),
            cadastro.NomeSolicitante.Trim(),
            cadastro.DataDeNascimento.ToString("O", CultureInfo.InvariantCulture),
            cadastro.RendaMensal.ToString(CultureInfo.InvariantCulture),
            cadastro.ValorSolicitado.ToString(CultureInfo.InvariantCulture),
            cadastro.PrazoEmMeses.ToString(CultureInfo.InvariantCulture),
            cadastro.Sistema.ToString());

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(campos)));
    }

    // O mesmo CPF com e sem pontuacao e o mesmo pedido.
    private static string SomenteDigitos(string cpf) =>
        string.Concat(cpf.Where(char.IsAsciiDigit));
}
