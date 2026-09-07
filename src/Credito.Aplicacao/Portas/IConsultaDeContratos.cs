using Credito.Aplicacao.Consultas;

namespace Credito.Aplicacao.Portas;

/// <summary>
/// Os contratos de uma conta bancaria, do lado de leitura.
/// </summary>
/// <remarks>
/// Nao passa pelo agregado pelo mesmo motivo da listagem de propostas: carregar
/// <see cref="Credito.Dominio.Contratos.Contrato"/> traria o cronograma inteiro por linha,
/// e uma conta com tres contratos de noventa e seis parcelas viraria duzentas e oitenta e
/// oito linhas de banco para exibir tres. A agregacao acontece no banco.
/// </remarks>
public interface IConsultaDeContratos
{
    Task<IReadOnlyList<ContratoDaConta>> DaConta(Guid contaId, CancellationToken cancelamento);
}
