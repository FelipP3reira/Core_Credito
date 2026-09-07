using System.Globalization;
using Credito.Dominio.Amortizacao;
using Credito.Dominio.Erros;

namespace Credito.Dominio.Contratos;

/// <summary>
/// O cronograma congelado no momento da contratacao.
/// </summary>
/// <remarks>
/// Ate aqui o cronograma era projecao: recalculado a cada consulta, com a taxa da politica
/// vigente naquele instante. Na contratacao ele vira divida, e por isso e gravado parcela a
/// parcela. Politica que mude depois nao pode alterar contrato ja assinado.
/// </remarks>
public sealed class Contrato
{
    public const int MinimoDeDiasAteOPrimeiroVencimento = 1;
    public const int MaximoDeDiasAteOPrimeiroVencimento = 60;

    private readonly List<ParcelaContratada> parcelas = [];

    private Contrato()
    {
    }

    private Contrato(
        Guid propostaId,
        Cronograma cronograma,
        DateOnly primeiroVencimento,
        DateTimeOffset assinadoEm,
        Guid? contaId)
    {
        Id = Guid.CreateVersion7();
        PropostaId = propostaId;
        ContaId = contaId;
        ValorFinanciado = cronograma.ValorFinanciado;
        TaxaMensal = cronograma.TaxaMensal;
        Sistema = cronograma.Sistema;
        PrazoEmMeses = cronograma.Parcelas.Count;
        PrimeiroVencimento = primeiroVencimento;
        AssinadoEm = assinadoEm;

        parcelas.AddRange(cronograma.Parcelas.Select(parcela => new ParcelaContratada(
            Id,
            parcela.Numero,
            VencimentoDa(parcela.Numero, primeiroVencimento),
            parcela.Amortizacao,
            parcela.Juros,
            parcela.Valor,
            parcela.SaldoDevedor)));
    }

    public Guid Id { get; private set; }

    public Guid PropostaId { get; private set; }

    /// <summary>
    /// A conta bancaria que recebe o desembolso e de onde saem as parcelas.
    /// </summary>
    /// <remarks>
    /// Opcional porque o contrato existe antes de haver conta: quem contrata sem informar
    /// conta assina uma divida que sera liquidada por fora, e o desembolso automatico
    /// simplesmente nao se aplica. O que nao pode existir e contrato com conta que nunca
    /// desembolsou e mesmo assim cobra parcela.
    /// </remarks>
    public Guid? ContaId { get; private set; }

    public decimal ValorFinanciado { get; private set; }

    public decimal TaxaMensal { get; private set; }

    public SistemaDeAmortizacao Sistema { get; private set; }

    public int PrazoEmMeses { get; private set; }

    public DateOnly PrimeiroVencimento { get; private set; }

    public DateTimeOffset AssinadoEm { get; private set; }

    public DateTimeOffset? DesembolsadoEm { get; private set; }

    /// <summary>A chave que credito a conta. Gravada para o reenvio se reconhecer.</summary>
    public string? ChaveDoDesembolso { get; private set; }

    /// <summary>O lancamento que o banco criou. E a ponta solta entre os dois sistemas.</summary>
    public Guid? LancamentoDoDesembolsoId { get; private set; }

    public bool EstaDesembolsado => DesembolsadoEm is not null;

    public IReadOnlyList<ParcelaContratada> Parcelas => parcelas;

    public bool EstaQuitado => parcelas.TrueForAll(parcela => parcela.EstaPaga);

    public decimal TotalPago => parcelas.Where(parcela => parcela.EstaPaga).Sum(parcela => parcela.Valor);

    public decimal SaldoAberto => parcelas.Where(parcela => !parcela.EstaPaga).Sum(parcela => parcela.Valor);

    public static Contrato Assinar(
        Guid propostaId,
        Cronograma cronograma,
        DateOnly primeiroVencimento,
        DateTimeOffset assinadoEm,
        Guid? contaId = null)
    {
        ArgumentNullException.ThrowIfNull(cronograma);

        GarantirPrimeiroVencimento(primeiroVencimento, DateOnly.FromDateTime(assinadoEm.UtcDateTime));

        return new Contrato(propostaId, cronograma, primeiroVencimento, assinadoEm, contaId);
    }

    /// <summary>
    /// A chave que o desembolso apresenta ao banco.
    /// </summary>
    /// <remarks>
    /// Derivada do contrato, e nao sorteada. E o que faz a operacao poder ser repetida sem
    /// medo: se a resposta do banco se perder no caminho, a nova tentativa chega com a
    /// mesma chave, o banco reconhece o lancamento que ja criou e devolve ele — em vez de
    /// creditar o emprestimo duas vezes. Chave sorteada a cada tentativa faria justamente o
    /// contrario.
    /// </remarks>
    public string ChaveDeDesembolso() =>
        string.Create(CultureInfo.InvariantCulture, $"credito:desembolso:{Id:N}");

    /// <summary>A chave que o pagamento da parcela apresenta ao banco.</summary>
    /// <remarks>Mesmo raciocinio da <see cref="ChaveDeDesembolso"/>, por parcela.</remarks>
    public string ChaveDaParcela(int numero) =>
        string.Create(CultureInfo.InvariantCulture, $"credito:parcela:{Id:N}:{numero}");

    /// <summary>
    /// Registra que o valor financiado entrou na conta.
    /// </summary>
    /// <remarks>
    /// Passo separado da assinatura de proposito. Assinar grava aqui; creditar grava no
    /// banco, que e outro sistema e outra transacao — nao ha como as duas coisas
    /// acontecerem ou deixarem de acontecer juntas. Separando, cada uma pode ser repetida
    /// ate dar certo, e o contrato assinado mas nao desembolsado e um estado visivel em vez
    /// de uma inconsistencia escondida.
    /// </remarks>
    public void Desembolsar(string chave, Guid lancamentoId, DateTimeOffset agora)
    {
        if (ContaId is null)
        {
            throw new ContratoInvalidoException("O contrato nao tem conta para receber o desembolso.");
        }

        if (EstaDesembolsado)
        {
            // Mesma chave e reenvio: nada muda. Chave diferente e uma segunda tentativa de
            // desembolsar o mesmo contrato, e isso e dinheiro em dobro.
            if (string.Equals(ChaveDoDesembolso, chave, StringComparison.Ordinal))
            {
                return;
            }

            throw new ContratoInvalidoException("O contrato ja foi desembolsado.");
        }

        DesembolsadoEm = agora;
        ChaveDoDesembolso = chave;
        LancamentoDoDesembolsoId = lancamentoId;
    }

    /// <summary>
    /// A parcela de numero <paramref name="numero"/>, ou recusa.
    /// </summary>
    /// <remarks>
    /// Publico porque quem cobra a parcela na conta precisa saber o valor antes de pagar —
    /// e precisa que um numero inexistente seja recusado antes de o dinheiro se mexer.
    /// </remarks>
    public ParcelaContratada Parcela(int numero) =>
        parcelas.Find(linha => linha.Numero == numero)
        ?? throw new ContratoInvalidoException($"O contrato nao tem parcela {numero}.");

    public ParcelaContratada Pagar(int numero, DateTimeOffset agora, string chaveDoPagamento)
    {
        if (string.IsNullOrWhiteSpace(chaveDoPagamento))
        {
            throw new ContratoInvalidoException("Chave do pagamento obrigatoria.");
        }

        var parcela = Parcela(numero);
        parcela.Pagar(agora, chaveDoPagamento);

        return parcela;
    }

    /// <summary>
    /// Vencimento da parcela, somando meses ao primeiro.
    /// </summary>
    /// <remarks>
    /// Somar meses, e nao trinta dias: parcela vence no mesmo dia do mes, nao a cada trinta
    /// dias corridos. <c>AddMonths</c> ja encurta o dia quando o mes seguinte nao o tem, e e
    /// isso que faz um contrato assinado com vencimento em 31 de janeiro cair em 28 ou 29 de
    /// fevereiro em vez de escorregar para marco.
    /// </remarks>
    private static DateOnly VencimentoDa(int numero, DateOnly primeiroVencimento) =>
        primeiroVencimento.AddMonths(numero - 1);

    private static void GarantirPrimeiroVencimento(DateOnly primeiroVencimento, DateOnly hoje)
    {
        var dias = primeiroVencimento.DayNumber - hoje.DayNumber;

        if (dias < MinimoDeDiasAteOPrimeiroVencimento)
        {
            throw new ContratoInvalidoException(
                $"O primeiro vencimento precisa ficar ao menos {MinimoDeDiasAteOPrimeiroVencimento} dia a frente da contratacao.");
        }

        if (dias > MaximoDeDiasAteOPrimeiroVencimento)
        {
            throw new ContratoInvalidoException(
                $"O primeiro vencimento nao pode passar de {MaximoDeDiasAteOPrimeiroVencimento} dias da contratacao.");
        }
    }
}
