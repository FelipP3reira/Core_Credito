namespace Credito.Aplicacao.Portas;

/// <summary>
/// Fecha a transacao de um caso de uso.
/// </summary>
/// <remarks>
/// Existe porque a contratacao mexe em dois repositorios — proposta e contrato — e um
/// Salvar em cada um deixaria ambiguo quem de fato grava. Aqui o caso de uso diz uma vez
/// que terminou, e tudo que ele tocou entra junto ou nao entra.
/// </remarks>
public interface IUnidadeDeTrabalho
{
    Task Salvar(CancellationToken cancelamento);
}
