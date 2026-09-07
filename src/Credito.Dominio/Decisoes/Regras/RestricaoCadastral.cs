namespace Credito.Dominio.Decisoes.Regras;

/// <summary>
/// Nome sujo em orgao de protecao ao credito reprova sozinho, sem compensacao possivel
/// por score alto ou renda folgada.
/// </summary>
public sealed class RestricaoCadastral : IRegraDeCredito
{
    public const string CodigoDaRegra = "RESTRICAO_CADASTRAL";

    public string Codigo => CodigoDaRegra;

    public VeredictoDaRegra Avaliar(ContextoDaAnalise contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        return contexto.TemRestricaoCadastral
            ? new VeredictoDaRegra(
                Codigo,
                Aprovou: false,
                "Solicitante com restricao cadastral ativa.")
            : new VeredictoDaRegra(
                Codigo,
                Aprovou: true,
                "Sem restricao cadastral.");
    }
}
