using Serilog.Core;
using Serilog.Events;

namespace Credito.Infraestrutura.Observabilidade;

/// <summary>
/// Substitui o valor de toda propriedade de log cujo nome soe sensivel, em qualquer
/// profundidade do evento.
/// </summary>
/// <remarks>
/// Filtrar por nome de propriedade, e nao por tipo, e o que faz isso valer: o vazamento
/// tipico nao e alguem logando um objeto de dominio, e alguem escrevendo
/// <c>logger.LogInformation("renda {Renda}", 8500m)</c> num diagnostico temporario que
/// nunca foi removido. Esse caso so e alcancavel pelo nome.
/// <para>
/// Isto e a rede de baixo, nao a primeira linha de defesa: o CPF ja se mascara sozinho
/// ao virar texto e a proposta nunca o guarda em claro.
/// </para>
/// </remarks>
public sealed class MascaraDeDadosSensiveis : ILogEventEnricher
{
    private const string Substituto = "[protegido]";

    private static readonly string[] NomesSensiveis =
    [
        "cpf",
        "renda",
        "salario",
        "senha",
        "password",
        "token",
        "authorization",
        "secret",
        "segredo",
        "pepper",
        "chave",
    ];

    // Nome dos parametros imposto pela assinatura de ILogEventEnricher.
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        foreach (var (nome, valor) in logEvent.Properties)
        {
            var limpo = EhSensivel(nome) ? new ScalarValue(Substituto) : Mascarar(valor);

            if (!ReferenceEquals(limpo, valor))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(nome, limpo));
            }
        }
    }

    // Devolve a mesma instancia quando nada mudou: assim o evento so e reescrito
    // quando havia de fato algo a esconder.
    private static LogEventPropertyValue Mascarar(LogEventPropertyValue valor) => valor switch
    {
        StructureValue estrutura => MascararEstrutura(estrutura),
        SequenceValue sequencia => MascararSequencia(sequencia),
        DictionaryValue dicionario => MascararDicionario(dicionario),
        _ => valor,
    };

    private static StructureValue MascararEstrutura(StructureValue estrutura)
    {
        var membros = new List<LogEventProperty>(estrutura.Properties.Count);
        var mudou = false;

        foreach (var membro in estrutura.Properties)
        {
            var limpo = EhSensivel(membro.Name) ? new ScalarValue(Substituto) : Mascarar(membro.Value);

            mudou |= !ReferenceEquals(limpo, membro.Value);
            membros.Add(new LogEventProperty(membro.Name, limpo));
        }

        return mudou ? new StructureValue(membros, estrutura.TypeTag) : estrutura;
    }

    private static SequenceValue MascararSequencia(SequenceValue sequencia)
    {
        var elementos = new List<LogEventPropertyValue>(sequencia.Elements.Count);
        var mudou = false;

        foreach (var elemento in sequencia.Elements)
        {
            var limpo = Mascarar(elemento);

            mudou |= !ReferenceEquals(limpo, elemento);
            elementos.Add(limpo);
        }

        return mudou ? new SequenceValue(elementos) : sequencia;
    }

    private static DictionaryValue MascararDicionario(DictionaryValue dicionario)
    {
        var entradas = new List<KeyValuePair<ScalarValue, LogEventPropertyValue>>(dicionario.Elements.Count);
        var mudou = false;

        foreach (var (chave, valor) in dicionario.Elements)
        {
            var limpo = chave.Value is string nome && EhSensivel(nome)
                ? new ScalarValue(Substituto)
                : Mascarar(valor);

            mudou |= !ReferenceEquals(limpo, valor);
            entradas.Add(new KeyValuePair<ScalarValue, LogEventPropertyValue>(chave, limpo));
        }

        return mudou ? new DictionaryValue(entradas) : dicionario;
    }

    private static bool EhSensivel(string nome) =>
        Array.Exists(NomesSensiveis, sensivel => nome.Contains(sensivel, StringComparison.OrdinalIgnoreCase));
}
