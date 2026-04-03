namespace ApiKeyManagement.SharedKernel.Domain;

public readonly struct Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    public bool IsFailure { get; }
    public bool IsSuccess => !IsFailure;

    public TValue Value
    {
        get
        {
            if (IsFailure) throw new InvalidOperationException("Cannot access Value of a failed Result.");
            return _value!;
        }
    }

    public TError Error
    {
        get
        {
            if (IsSuccess) throw new InvalidOperationException("Cannot access Error of a successful Result.");
            return _error!;
        }
    }

    private Result(TValue value)
    {
        _value = value;
        _error = default;
        IsFailure = false;
    }

    private Result(TError error)
    {
        _value = default;
        _error = error;
        IsFailure = true;
    }

    public static implicit operator Result<TValue, TError>(TValue value) => new(value);
    public static implicit operator Result<TValue, TError>(TError error) => new(error);
}
