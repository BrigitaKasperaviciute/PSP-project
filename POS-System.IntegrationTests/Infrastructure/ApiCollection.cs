using Xunit;

namespace POS_System.IntegrationTests.Infrastructure;

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiTestFactory>
{
}
