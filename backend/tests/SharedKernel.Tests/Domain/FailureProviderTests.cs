using ApiKeyManagement.SharedKernel.Domain;
using FluentAssertions;

namespace ApiKeyManagement.SharedKernel.Tests.Domain;

public class FailureProviderTests
{
    [Fact]
    public void CreateFailure_WithValidCode_ReturnsFailureWithCode()
    {
        var failure = FailureProvider.CreateFailure("TENANT_NOT_FOUND");

        failure.Code.Should().Be("TENANT_NOT_FOUND");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void CreateFailure_WithNullOrWhitespace_Throws(string? code)
    {
        var act = () => FailureProvider.CreateFailure(code!);

        act.Should().Throw<ArgumentException>();
    }
}
