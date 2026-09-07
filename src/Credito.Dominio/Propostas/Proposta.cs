using Credito.Dominio.Amortizacao;
using Credito.Dominio.Comum;
using Credito.Dominio.Decisoes;
using Credito.Dominio.Erros;

namespace Credito.Dominio.Propostas;

/// <summary>
/// Raiz do agregado. O estado so muda pelos metodos de intencao desta classe, e cada
/// um deles passa por <see cref="MaquinaDeEstados.GarantirTransicao"/> antes de gravar
/// a transicao no historico. Nao existe setter publico de <see cref="Estado"/>.
/// </summary>
public sealed class Proposta
{
    public const int IdadeMinima = 18;
    public const int TamanhoMaximoDoNome = 150;

    private readonly List<TransicaoDeEstado> transicoes = [];
    private readonly List<Decisao> decisoes = [];

    private Proposta()
    {
    }

    private Proposta(DadosDaProposta dados, DateTimeOffset criadaEm)
    {
        // GUID v7 e ordenado no tempo: como o SQL Server usa o indice agrupado da
        // chave primaria para ordenar as linhas em disco, GUID aleatorio espalharia
        // insercao por todas as paginas e fragmentaria o indice.
        Id = Guid.CreateVersion7();
        ChaveIdempotencia = dados.ChaveIdempotencia;
        ImpressaoDoPedido = dados.ImpressaoDoPedido;
        Cpf = dados.Cpf;
        NomeSolicitante = dados.NomeSolicitante.Trim();
        DataDeNascimento = dados.DataDeNascimento;
        RendaMensal = dados.RendaMensal;
        ValorSolicitado = dados.ValorSolicitado;
        PrazoEmMeses = dados.PrazoEmMeses;
        Sistema = dados.Sistema;
        Estado = EstadoDaProposta.Rascunho;
        CriadaEm = criadaEm;
        AtualizadaEm = criadaEm;
    }

    public Guid Id { get; private set; }

    public string ChaveIdempotencia { get; private set; } = string.Empty;

    /// <summary>
    /// Resumo do conteudo enviado. Mesma chave com impressao diferente e erro do
    /// cliente, nao reenvio — e precisa ser recusado em vez de devolver a proposta errada.
    /// </summary>
    public string ImpressaoDoPedido { get; private set; } = string.Empty;

    public CpfProtegido Cpf { get; private set; } = null!;

    public string NomeSolicitante { get; private set; } = string.Empty;

    public DateOnly DataDeNascimento { get; private set; }

    public decimal RendaMensal { get; private set; }

    public decimal ValorSolicitado { get; private set; }

    public int PrazoEmMeses { get; private set; }

    public SistemaDeAmortizacao Sistema { get; private set; }

    public EstadoDaProposta Estado { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    public DateTimeOffset AtualizadaEm { get; private set; }

    /// <summary>Controle de concorrencia otimista: duas analises simultaneas nao podem ambas vencer.</summary>
    public byte[] Versao { get; private set; } = [];

    public IReadOnlyList<TransicaoDeEstado> Transicoes => transicoes;

    /// <summary>
    /// A maquina de estados so permite uma decisao por proposta — negada e terminal e
    /// aprovada segue para contratacao. E colecao porque o registro e append-only: se um
    /// dia couber reanalise, ela entra ao lado da anterior em vez de apagar.
    /// </summary>
    public IReadOnlyList<Decisao> Decisoes => decisoes;

    public static Proposta Rascunho(DadosDaProposta dados, DateTimeOffset agora)
    {
        Validar(dados, DateOnly.FromDateTime(agora.UtcDateTime));
        return new Proposta(dados, agora);
    }

    public void EnviarParaAnalise(DateTimeOffset agora, string origem) =>
        Transitar(EstadoDaProposta.EmAnalise, agora, origem);

    public void Cancelar(DateTimeOffset agora, string origem) =>
        Transitar(EstadoDaProposta.Cancelada, agora, origem);

    public void Aprovar(Decisao decisao, DateTimeOffset agora, string origem) =>
        Decidir(decisao, EstadoDaProposta.Aprovada, agora, origem);

    public void Negar(Decisao decisao, DateTimeOffset agora, string origem) =>
        Decidir(decisao, EstadoDaProposta.Negada, agora, origem);

    /// <summary>
    /// Aprovar e negar exigem o laudo na assinatura. Nao e documentacao: e o compilador
    /// impedindo que exista caminho de codigo capaz de decidir sem registrar por que.
    /// </summary>
    private void Decidir(Decisao decisao, EstadoDaProposta destino, DateTimeOffset agora, string origem)
    {
        ArgumentNullException.ThrowIfNull(decisao);

        if (decisao.PropostaId != Id)
        {
            throw new PropostaInvalidaException("O laudo apresentado e de outra proposta.");
        }

        var concluido = decisao.Aprovada ? EstadoDaProposta.Aprovada : EstadoDaProposta.Negada;
        if (concluido != destino)
        {
            throw new PropostaInvalidaException(
                $"O laudo conclui {concluido} e a transicao pedida foi {destino}.");
        }

        // Transitar primeiro: e ele que pode recusar. Guardar o laudo antes deixaria o
        // parecer preso a uma proposta que nunca mudou de estado.
        Transitar(destino, agora, origem);
        decisoes.Add(decisao);
    }

    private void Transitar(EstadoDaProposta destino, DateTimeOffset agora, string origem)
    {
        MaquinaDeEstados.GarantirTransicao(Estado, destino);

        transicoes.Add(new TransicaoDeEstado(Id, transicoes.Count + 1, Estado, destino, agora, origem));
        Estado = destino;
        AtualizadaEm = agora;
    }

    private static void Validar(DadosDaProposta dados, DateOnly hoje)
    {
        if (string.IsNullOrWhiteSpace(dados.ChaveIdempotencia))
        {
            throw new PropostaInvalidaException("Chave de idempotencia obrigatoria.");
        }

        if (string.IsNullOrWhiteSpace(dados.ImpressaoDoPedido))
        {
            throw new PropostaInvalidaException("Impressao do pedido obrigatoria.");
        }

        if (string.IsNullOrWhiteSpace(dados.NomeSolicitante))
        {
            throw new PropostaInvalidaException("Nome do solicitante obrigatorio.");
        }

        if (dados.NomeSolicitante.Trim().Length > TamanhoMaximoDoNome)
        {
            throw new PropostaInvalidaException($"Nome do solicitante passa de {TamanhoMaximoDoNome} caracteres.");
        }

        if (dados.RendaMensal <= 0)
        {
            throw new PropostaInvalidaException("Renda mensal precisa ser maior que zero.");
        }

        if (dados.ValorSolicitado <= 0)
        {
            throw new PropostaInvalidaException("Valor solicitado precisa ser maior que zero.");
        }

        if (dados.PrazoEmMeses < 1)
        {
            throw new PropostaInvalidaException("Prazo precisa ser de ao menos um mes.");
        }

        // Capacidade civil e lei, nao politica de credito: nao vira regra configuravel
        // do motor, vira invariante do agregado.
        if (IdadeEm(dados.DataDeNascimento, hoje) < IdadeMinima)
        {
            throw new PropostaInvalidaException($"Solicitante precisa ter ao menos {IdadeMinima} anos.");
        }
    }

    private static int IdadeEm(DateOnly nascimento, DateOnly data)
    {
        var idade = data.Year - nascimento.Year;
        return data < nascimento.AddYears(idade) ? idade - 1 : idade;
    }
}
