namespace ApiKeyManagement.SharedKernel.Domain;

public static class FailureProvider
{
    public static Failure CreateFailure(string code) => new(code);
}
