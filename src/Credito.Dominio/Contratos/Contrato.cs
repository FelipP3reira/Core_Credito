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

    private Contrato(Guid propostaId, Cronograma cronograma, DateOnly primeiroVencimento, DateTimeOffset assinadoEm)
    {
        Id = Guid.CreateVersion7();
        PropostaId = propostaId;
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

    public decimal ValorFinanciado { get; private set; }

    public decimal TaxaMensal { get; private set; }

    public SistemaDeAmortizacao Sistema { get; private set; }

    public int PrazoEmMeses { get; private set; }

    public DateOnly PrimeiroVencimento { get; private set; }

    public DateTimeOffset AssinadoEm { get; private set; }

    public IReadOnlyList<ParcelaContratada> Parcelas => parcelas;

    public bool EstaQuitado => parcelas.TrueForAll(parcela => parcela.EstaPaga);

    public decimal TotalPago => parcelas.Where(parcela => parcela.EstaPaga).Sum(parcela => parcela.Valor);

    public decimal SaldoAberto => parcelas.Where(parcela => !parcela.EstaPaga).Sum(parcela => parcela.Valor);

    public static Contrato Assinar(
        Guid propostaId,
        Cronograma cronograma,
        DateOnly primeiroVencimento,
        DateTimeOffset assinadoEm)
    {
        ArgumentNullException.ThrowIfNull(cronograma);

        GarantirPrimeiroVencimento(primeiroVencimento, DateOnly.FromDateTime(assinadoEm.UtcDateTime));

        return new Contrato(propostaId, cronograma, primeiroVencimento, assinadoEm);
    }

    public ParcelaContratada Pagar(int numero, DateTimeOffset agora, string chaveDoPagamento)
    {
        if (string.IsNullOrWhiteSpace(chaveDoPagamento))
        {
            throw new ContratoInvalidoException("Chave do pagamento obrigatoria.");
        }

        var parcela = parcelas.Find(linha => linha.Numero == numero)
            ?? throw new ContratoInvalidoException($"O contrato nao tem parcela {numero}.");

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
