using System.Text.Json;
using System.Text.Json.Serialization;

namespace Credito.Testes.Integracao;

internal static class Pedidos
{
    public const string CpfValido = "529.982.247-25";
    public const string CpfEmDigitos = "52998224725";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static object Cadastro(
        decimal valorSolicitado = 20_000m,
        string cpf = CpfValido,
        string nomeSolicitante = "Ana Ribeiro",
        string dataDeNascimento = "1994-03-12",
        decimal rendaMensal = 8_500m,
        int prazoEmMeses = 24,
        string sistema = "Price") =>
        new
        {
            cpf,
            nomeSolicitante,
            dataDeNascimento,
            rendaMensal,
            valorSolicitado,
            prazoEmMeses,
            sistema,
        };

    public static string ChaveNova() => Guid.NewGuid().ToString();
}
