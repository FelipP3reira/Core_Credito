using System.ComponentModel.DataAnnotations;

namespace Credito.Infraestrutura.Banco;

/// <summary>
/// Onde fica a Plataforma Bancaria e quanto tempo esperar por ela.
/// </summary>
/// <remarks>
/// Tempo limite curto de proposito. A chamada acontece no meio de um pedido HTTP que alguem
/// esta esperando, e o desenho ja assume que ela pode nao chegar: com a chave derivada do
/// contrato, desistir e tentar de novo e seguro. Esperar meio minuto seria segurar a
/// requisicao do cliente para chegar na mesma conclusao.
/// </remarks>
public sealed class OpcoesDaContaBancaria
{
    public const string Secao = "ContaBancaria";

    [Required]
    [Url]
    public string BaseUrl { get; init; } = string.Empty;

    [Range(1, 60)]
    public int TempoLimiteEmSegundos { get; init; } = 10;
}
