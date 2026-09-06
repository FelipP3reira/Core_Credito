using Credito.Dominio.Amortizacao;
using Credito.Dominio.Comum;
using Credito.Dominio.Propostas;

namespace Credito.Testes.Unidade.Propostas;

internal static class DadosDeExemplo
{
    public static readonly DateTimeOffset Agora = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    // O dominio so ve strings opacas: como elas foram geradas nao importa aqui.
    public static readonly CpfProtegido CpfDeExemplo = new("hash-de-teste", "cifrado-de-teste");

    public static DadosDaProposta Validos(
        string chaveIdempotencia = "3f9a1c7e-2b44-4d0a-9c31-5e6d8f0a1b22",
        string impressaoDoPedido = "impressao-de-teste",
        string nomeSolicitante = "Ana Ribeiro",
        DateOnly? dataDeNascimento = null,
        decimal rendaMensal = 6_500m,
        decimal valorSolicitado = 20_000m,
        int prazoEmMeses = 24,
        SistemaDeAmortizacao sistema = SistemaDeAmortizacao.Price) =>
        new(
            chaveIdempotencia,
            impressaoDoPedido,
            CpfDeExemplo,
            nomeSolicitante,
            dataDeNascimento ?? new DateOnly(1994, 3, 12),
            rendaMensal,
            valorSolicitado,
            prazoEmMeses,
            sistema);

    public static Proposta EmAnalise()
    {
        var proposta = Proposta.Rascunho(Validos(), Agora);
        proposta.EnviarParaAnalise(Agora, "teste");
        return proposta;
    }
}
