namespace ApiKeyManagement.SharedKernel.Domain;

public static class FailureProvider
{
    public static Failure CreateFailure(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new Failure(code);
    }
}
