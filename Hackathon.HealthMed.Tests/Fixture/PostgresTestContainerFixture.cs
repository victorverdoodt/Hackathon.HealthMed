using Testcontainers.PostgreSql;

namespace Hackathon.HealthMed.Tests.Fixture
{
    public class PostgresTestContainerFixture : IAsyncLifetime
    {
        // Cria o container PostgreSQL utilizando o builder da nova biblioteca.
        private readonly PostgreSqlContainer _postgreSqlContainer = new PostgreSqlBuilder()
            .WithDatabase("testdb_unit")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithImage("postgres:14-alpine")
            .Build();

        // Inicia o container antes dos testes.
        public Task InitializeAsync() => _postgreSqlContainer.StartAsync();

        // Libera os recursos e para o container após os testes.
        public Task DisposeAsync() => _postgreSqlContainer.DisposeAsync().AsTask();

        // Expondo a string de conexão.
        public string GetConnectionString() => _postgreSqlContainer.GetConnectionString();
    }

    // Define uma coleção para garantir que os testes que utilizam o container sejam executados em série.
    [CollectionDefinition("Postgres Unit Collection")]
    public class PostgresUnitCollection : ICollectionFixture<PostgresTestContainerFixture> { }
}
