namespace Credito.Dominio.Amortizacao;

/// <param name="Amortizacao">Parte da parcela que abate o saldo devedor.</param>
/// <param name="Juros">Parte que remunera o credor e nao reduz nada.</param>
/// <param name="Valor">Soma das duas — o que o cliente paga no mes.</param>
/// <param name="SaldoDevedor">O que sobra devendo depois desta parcela.</param>
public sealed record Parcela(
    int Numero,
    decimal Amortizacao,
    decimal Juros,
    decimal Valor,
    decimal SaldoDevedor);
