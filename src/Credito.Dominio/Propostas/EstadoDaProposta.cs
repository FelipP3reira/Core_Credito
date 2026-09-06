namespace Credito.Dominio.Propostas;

/// <summary>
/// Valores fixados explicitamente: o estado e persistido como inteiro e reordenar
/// o enum nao pode reescrever o historico ja gravado.
/// </summary>
public enum EstadoDaProposta
{
    Rascunho = 1,
    EmAnalise = 2,
    Aprovada = 3,
    Negada = 4,
    Contratada = 5,
    Liquidada = 6,
    Cancelada = 7,
    Expirada = 8,
}
