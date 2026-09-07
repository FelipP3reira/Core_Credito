using Credito.Dominio.Politicas;

namespace Credito.Aplicacao.Portas;

public interface IRepositorioDePoliticas
{
    /// <summary>Politica de maior versao ja vigente na data informada.</summary>
    Task<PoliticaDeCredito?> Vigente(DateTimeOffset em, CancellationToken cancelamento);
}
