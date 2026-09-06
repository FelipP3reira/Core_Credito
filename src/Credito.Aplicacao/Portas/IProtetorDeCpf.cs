using Credito.Dominio.Comum;

namespace Credito.Aplicacao.Portas;

/// <summary>
/// Unico caminho entre o CPF em texto claro e a forma que a proposta guarda.
/// Implementado na Infraestrutura; o dominio nao conhece o algoritmo.
/// </summary>
public interface IProtetorDeCpf
{
    CpfProtegido Proteger(Cpf cpf);

    /// <summary>
    /// Devolve o numero em texto claro. Nome deliberadamente chamativo: toda chamada
    /// e um ponto de vazamento em potencial e precisa saltar aos olhos na revisao.
    /// </summary>
    Cpf Revelar(CpfProtegido protegido);
}
