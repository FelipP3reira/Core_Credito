using System.Globalization;

namespace Credito.Dominio.Decisoes;

/// <summary>
/// Formata os numeros que aparecem no motivo de cada veredicto.
/// </summary>
/// <remarks>
/// Cultura fixada em pt-BR, e nao herdada da maquina: o motivo e gravado como texto e fica
/// no laudo para sempre, entao nao pode sair com ponto decimal so porque o servidor subiu
/// com outra configuracao regional.
/// </remarks>
internal static class TextoDoLaudo
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    public static string Dinheiro(decimal valor) => valor.ToString("C2", Brasil);

    public static string Percentual(decimal fracao) =>
        (fracao * 100).ToString("0.##", Brasil) + "%";

    public static string Meses(int quantidade) => quantidade == 1 ? "1 mes" : $"{quantidade} meses";
}
