namespace Credito.Dominio.Decisoes;

/// <summary>
/// O que uma regra concluiu, e com base em que numero.
/// </summary>
/// <remarks>
/// Uma Specification devolvendo bool nao serve aqui. O requisito de compliance e guardar
/// POR QUE a proposta foi negada, e um booleano perde isso no caminho — seis meses depois
/// ninguem consegue reconstruir se faltou score ou se a parcela pesava demais.
/// <para>
/// Os numeros sao nulos nas regras que nao tem numero, como restricao cadastral. Fingir
/// um valor ali so para preencher a coluna atrapalharia quem for consultar depois.
/// </para>
/// </remarks>
public sealed record VeredictoDaRegra(
    string Codigo,
    bool Aprovou,
    string Motivo,
    decimal? ValorObservado = null,
    decimal? LimiteExigido = null);
