using Xunit;

namespace POS_System.IntegrationTests.TestSupport;

[CollectionDefinition("Integration tests", DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<ApiTestFactory>
{
}
