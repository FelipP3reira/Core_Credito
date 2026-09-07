using Credito.Dominio.Contratos;

namespace Credito.Aplicacao.Portas;

public interface IRepositorioDeContratos
{
    Task<Contrato?> PorProposta(Guid propostaId, CancellationToken cancelamento);

    void Adicionar(Contrato contrato);
}
