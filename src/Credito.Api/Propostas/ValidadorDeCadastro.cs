using Credito.Dominio.Comum;
using Credito.Dominio.Propostas;
using FluentValidation;

namespace Credito.Api.Propostas;

/// <summary>
/// Validacao da borda. Repete de proposito parte do que o agregado ja garante: aqui
/// o objetivo e devolver ao cliente qual campo esta errado e por que; la e impedir
/// que exista proposta invalida, venha de onde vier.
/// </summary>
public sealed class ValidadorDeCadastro : AbstractValidator<PedidoDeCadastro>
{
    // Teto para separar erro de digitacao de pedido de verdade: sem ele, um zero a
    // mais vira uma proposta de dez milhoes que so o motor de credito vai recusar.
    private const decimal ValorMaximo = 1_000_000m;
    private const decimal RendaMaxima = 1_000_000m;
    private const int PrazoMaximoEmMeses = 480;

    public ValidadorDeCadastro(TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);

        RuleFor(pedido => pedido.Cpf)
            .NotEmpty().WithMessage("Informe o CPF.")
            .Must(cpf => Cpf.TentarCriar(cpf, out _)).WithMessage("Esse CPF nao existe — confira os numeros.");

        RuleFor(pedido => pedido.NomeSolicitante)
            .NotEmpty().WithMessage("Informe o nome do solicitante.")
            .MaximumLength(Proposta.TamanhoMaximoDoNome)
            .WithMessage($"O nome passa de {Proposta.TamanhoMaximoDoNome} caracteres.");

        RuleFor(pedido => pedido.DataDeNascimento)
            .Must(nascimento => nascimento < DateOnly.FromDateTime(relogio.GetUtcNow().UtcDateTime))
            .WithMessage("A data de nascimento precisa estar no passado.")
            .Must(nascimento => TemIdadeMinima(nascimento, relogio))
            .WithMessage($"O solicitante precisa ter ao menos {Proposta.IdadeMinima} anos.");

        RuleFor(pedido => pedido.RendaMensal)
            .GreaterThan(0).WithMessage("A renda mensal precisa ser maior que zero.")
            .LessThanOrEqualTo(RendaMaxima).WithMessage("Confira a renda informada — o valor esta fora da faixa aceita.");

        RuleFor(pedido => pedido.ValorSolicitado)
            .GreaterThan(0).WithMessage("O valor solicitado precisa ser maior que zero.")
            .LessThanOrEqualTo(ValorMaximo).WithMessage("Confira o valor pedido — esta fora da faixa aceita.");

        RuleFor(pedido => pedido.PrazoEmMeses)
            .InclusiveBetween(1, PrazoMaximoEmMeses)
            .WithMessage($"O prazo precisa ficar entre 1 e {PrazoMaximoEmMeses} meses.");

        RuleFor(pedido => pedido.Sistema)
            .IsInEnum().WithMessage("Sistema de amortizacao invalido. Use Price ou Sac.");
    }

    private static bool TemIdadeMinima(DateOnly nascimento, TimeProvider relogio)
    {
        var hoje = DateOnly.FromDateTime(relogio.GetUtcNow().UtcDateTime);
        var idade = hoje.Year - nascimento.Year;

        return (hoje < nascimento.AddYears(idade) ? idade - 1 : idade) >= Proposta.IdadeMinima;
    }
}
