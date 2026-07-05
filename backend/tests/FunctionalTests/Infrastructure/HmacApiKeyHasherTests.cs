using ApiKeyManagement.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace ApiKeyManagement.FunctionalTests.Infrastructure;

/// <summary>
/// Unit-style tests for the ADR-017 key hasher — no DI container needed, construct directly.
/// </summary>
public class HmacApiKeyHasherTests
{
    private const string ValidPepperBase64 = "ZGV2ZWxvcG1lbnQtb25seS1wZXBwZXItMzJieXRlcyE="; // 32 bytes
    private const string OtherValidPepperBase64 = "ZnVuY3Rpb25hbC10ZXN0LXBlcHBlci0zMmJ5dGVzISE="; // 32 bytes

    private static HmacApiKeyHasher CreateHasher(string pepperBase64)
        => new(Options.Create(new ApiKeyHashingOptions { Pepper = pepperBase64 }));

    [Fact]
    public void ComputeHash_SameInputSamePepper_IsDeterministic()
    {
        var hasher = CreateHasher(ValidPepperBase64);

        var first = hasher.ComputeHash("apk_tena_prod_abcdef1234567890_abcd");
        var second = hasher.ComputeHash("apk_tena_prod_abcdef1234567890_abcd");

        first.Should().Be(second);
    }

    [Fact]
    public void ComputeHash_DifferentPepper_ProducesDifferentOutput()
    {
        var hasherA = CreateHasher(ValidPepperBase64);
        var hasherB = CreateHasher(OtherValidPepperBase64);

        var rawKey = "apk_tena_prod_abcdef1234567890_abcd";

        hasherA.ComputeHash(rawKey).Should().NotBe(hasherB.ComputeHash(rawKey));
    }

    [Fact]
    public void ComputeHash_Output_IsBase64Of32Bytes()
    {
        var hasher = CreateHasher(ValidPepperBase64);

        var hash = hasher.ComputeHash("apk_tena_prod_abcdef1234567890_abcd");

        Convert.FromBase64String(hash).Length.Should().Be(32);
    }

    [Fact]
    public void Constructor_PepperNotBase64_ThrowsInvalidOperationException()
    {
        var act = () => CreateHasher("not-valid-base64!!!");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_PepperShorterThan32Bytes_ThrowsInvalidOperationException()
    {
        // Valid Base64, decodes to fewer than 32 bytes.
        var shortPepper = Convert.ToBase64String("too-short"u8.ToArray());

        var act = () => CreateHasher(shortPepper);

        act.Should().Throw<InvalidOperationException>();
    }
}
