using Credito.Dominio.Amortizacao;
using Credito.Dominio.Propostas;

namespace Credito.Aplicacao.Consultas;

/// <summary>
/// Uma proposta na listagem.
/// </summary>
/// <remarks>
/// Sem CPF, nem mascarado. A mascara so existe depois de decifrar, entao uma pagina de
/// cem linhas decifraria cem CPFs para mostrar tres digitos de cada — chave de cifra
/// exercitada cem vezes e cem numeros em texto claro na memoria do processo, em troca de
/// nada que a listagem precise. Quem tem que identificar a pessoa abre a proposta.
/// Efeito colateral util: o caminho de leitura em lote nao toca na chave de cifra.
/// </remarks>
public sealed record LinhaDaProposta(
    Guid Id,
    EstadoDaProposta Estado,
    string NomeSolicitante,
    decimal ValorSolicitado,
    int PrazoEmMeses,
    SistemaDeAmortizacao Sistema,
    DateTimeOffset CriadaEm,
    DateTimeOffset AtualizadaEm);

/// <param name="CpfHash">
/// Ja resolvido pelo protetor. A consulta procura pelo indice do hash e nunca vê o numero.
/// </param>
/// <param name="Tamanho">Quantas linhas trazer, no maximo.</param>
public sealed record FiltroDeLeitura(
    EstadoDaProposta? Estado,
    DateTimeOffset? De,
    DateTimeOffset? Ate,
    string? CpfHash,
    MarcadorDaPagina? Continuacao,
    int Tamanho);

public sealed record ContagemPorEstado(EstadoDaProposta Estado, int Quantidade, decimal ValorSolicitado);
