using FluentValidation;

namespace Credito.Api.Propostas;

/// <param name="TaxaMensal">Fracao ao mes: 0,0189 e 1,89% a.m.</param>
public sealed record PedidoDeSimulacao(decimal TaxaMensal);

public sealed class ValidadorDeSimulacao : AbstractValidator<PedidoDeSimulacao>
{
    // Vinte por cento ao mes ja e mais do que qualquer linha de credito legal no pais.
    // Acima disso e erro de unidade: alguem mandou 1,89 querendo dizer 1,89 por cento.
    private const decimal TaxaMaxima = 0.20m;

    public ValidadorDeSimulacao() =>
        RuleFor(pedido => pedido.TaxaMensal)
            .GreaterThanOrEqualTo(0).WithMessage("A taxa mensal nao pode ser negativa.")
            .LessThanOrEqualTo(TaxaMaxima)
            .WithMessage("A taxa mensal esta fora da faixa aceita. Informe a fracao ao mes: 0,0189 para 1,89%.");
}
