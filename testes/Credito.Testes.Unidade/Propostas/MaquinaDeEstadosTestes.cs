using Credito.Dominio.Erros;
using Credito.Dominio.Propostas;

namespace Credito.Testes.Unidade.Propostas;

public class MaquinaDeEstadosTestes
{
    /// <summary>
    /// Espelho independente da tabela de producao: escrito a partir do desenho do fluxo,
    /// nao copiado do codigo. Se as duas divergirem, o teste exaustivo abaixo acusa.
    /// </summary>
    private static readonly (EstadoDaProposta De, EstadoDaProposta Para)[] ArestasValidas =
    [
        (EstadoDaProposta.Rascunho, EstadoDaProposta.EmAnalise),
        (EstadoDaProposta.Rascunho, EstadoDaProposta.Cancelada),
        (EstadoDaProposta.EmAnalise, EstadoDaProposta.Aprovada),
        (EstadoDaProposta.EmAnalise, EstadoDaProposta.Negada),
        (EstadoDaProposta.EmAnalise, EstadoDaProposta.Cancelada),
        (EstadoDaProposta.Aprovada, EstadoDaProposta.Contratada),
        (EstadoDaProposta.Aprovada, EstadoDaProposta.Expirada),
        (EstadoDaProposta.Contratada, EstadoDaProposta.Liquidada),
    ];

    private static readonly EstadoDaProposta[] Terminais =
    [
        EstadoDaProposta.Negada,
        EstadoDaProposta.Liquidada,
        EstadoDaProposta.Cancelada,
        EstadoDaProposta.Expirada,
    ];

    public static TheoryData<EstadoDaProposta, EstadoDaProposta> TodosOsPares()
    {
        var pares = new TheoryData<EstadoDaProposta, EstadoDaProposta>();

        foreach (var de in Enum.GetValues<EstadoDaProposta>())
        {
            foreach (var para in Enum.GetValues<EstadoDaProposta>())
            {
                pares.Add(de, para);
            }
        }

        return pares;
    }

    [Theory]
    [MemberData(nameof(TodosOsPares))]
    public void PermiteExatamenteAsArestasDesenhadas(EstadoDaProposta de, EstadoDaProposta para)
    {
        var esperado = Array.IndexOf(ArestasValidas, (de, para)) >= 0;

        Assert.Equal(esperado, MaquinaDeEstados.PodeTransitar(de, para));
    }

    [Fact]
    public void NenhumEstadoTransitaParaSiMesmo()
    {
        foreach (var estado in Enum.GetValues<EstadoDaProposta>())
        {
            Assert.False(MaquinaDeEstados.PodeTransitar(estado, estado));
        }
    }

    [Theory]
    [MemberData(nameof(TodosOsPares))]
    public void GarantirTransicaoLancaExatamenteNasArestasProibidas(EstadoDaProposta de, EstadoDaProposta para)
    {
        if (MaquinaDeEstados.PodeTransitar(de, para))
        {
            MaquinaDeEstados.GarantirTransicao(de, para);
            return;
        }

        var erro = Assert.Throws<TransicaoInvalidaException>(() => MaquinaDeEstados.GarantirTransicao(de, para));

        Assert.Equal(de, erro.De);
        Assert.Equal(para, erro.Para);
    }

    [Fact]
    public void ReconheceOsEstadosTerminais()
    {
        foreach (var estado in Enum.GetValues<EstadoDaProposta>())
        {
            var esperado = Array.IndexOf(Terminais, estado) >= 0;

            Assert.Equal(esperado, MaquinaDeEstados.EhTerminal(estado));
        }
    }

    /// <summary>
    /// Pega estado novo adicionado ao enum que ninguem ligou na tabela: ele compila,
    /// passa nos outros testes e nunca acontece.
    /// </summary>
    [Fact]
    public void TodoEstadoEhAlcancavelAPartirDoRascunho()
    {
        var alcancados = new HashSet<EstadoDaProposta> { EstadoDaProposta.Rascunho };
        var aVisitar = new Queue<EstadoDaProposta>([EstadoDaProposta.Rascunho]);

        while (aVisitar.Count > 0)
        {
            foreach (var destino in MaquinaDeEstados.SaidasDe(aVisitar.Dequeue()))
            {
                if (alcancados.Add(destino))
                {
                    aVisitar.Enqueue(destino);
                }
            }
        }

        Assert.Equal(Enum.GetValues<EstadoDaProposta>().Length, alcancados.Count);
    }
}
