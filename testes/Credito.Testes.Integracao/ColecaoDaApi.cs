namespace Credito.Testes.Integracao;

/// <summary>
/// Um container de SQL Server para a suite inteira. Por classe, cada uma pagaria a
/// subida do banco de novo — mais de meio minuto cada.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class ColecaoDaApi : ICollectionFixture<FabricaDeApi>
{
    public const string Nome = "api";
}
