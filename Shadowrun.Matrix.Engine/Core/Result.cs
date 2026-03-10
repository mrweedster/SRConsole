namespace Shadowrun.Matrix.Core;

/// <summary>
/// Represents the outcome of an operation that can fail in an expected way.
/// Use this instead of exceptions when failure is a normal part of the game flow
/// (e.g. a program run that misses, a load that would exceed memory limits).
///
/// Use <c>throw</c> for programming errors (null arguments, invalid state that
/// should never occur at runtime).
/// </summary>
public class Result
{
    // ── State ────────────────────────────────────────────────────────────────

    public bool   IsSuccess { get; }
    public bool   IsFailure => !IsSuccess;
    public string Error     { get; }

    // ── Constructors (private — use factory methods) ─────────────────────────

    protected Result(bool isSuccess, string error)
    {
        if (isSuccess && error != string.Empty)
            throw new InvalidOperationException(
                "A successful result must not carry an error message.");

        if (!isSuccess && string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException(
                "A failed result must carry a non-empty error message.");

        IsSuccess = isSuccess;
        Error     = error;
    }

    // ── Factory methods ──────────────────────────────────────────────────────

    /// <summary>Creates a successful result with no payload.</summary>
    public static Result Ok() => new(true, string.Empty);

    /// <summary>Creates a failed result with a descriptive error message.</summary>
    public static Result Fail(string error) => new(false, error);

    /// <summary>Creates a successful result carrying a value.</summary>
    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);

    /// <summary>Creates a failed result of a given type.</summary>
    public static Result<T> Fail<T>(string error) => Result<T>.Fail(error);

    // ── Convenience ──────────────────────────────────────────────────────────

    public override string ToString() =>
        IsSuccess ? "Result: OK" : $"Result: FAIL — {Error}";
}

/// <summary>
/// Typed variant of <see cref="Result"/>. Carries a value on success;
/// the value is <c>default</c> (and must not be read) on failure.
/// </summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    // ── State ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The operation's return value. Only valid when <see cref="Result.IsSuccess"/> is true.
    /// Throws <see cref="InvalidOperationException"/> if accessed on a failed result.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot access Value on a failed result. Error: {Error}");

    // ── Constructors (private — use factory methods) ─────────────────────────

    private Result(T value) : base(true, string.Empty) => _value = value;
    private Result(string error) : base(false, error)  => _value = default;

    // ── Factory methods ──────────────────────────────────────────────────────

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    public static Result<T> Ok(T value) => new(value);

    /// <summary>Creates a failed result with a descriptive error message.</summary>
    public new static Result<T> Fail(string error) => new(error);

    // ── Implicit conversion ──────────────────────────────────────────────────

    /// <summary>
    /// Allows returning a bare <typeparamref name="T"/> from a method that
    /// returns <see cref="Result{T}"/> without explicitly calling Ok().
    /// <code>return myValue;  // implicitly wraps in Result&lt;T&gt;.Ok()</code>
    /// </summary>
    public static implicit operator Result<T>(T value) => Ok(value);

    public override string ToString() =>
        IsSuccess ? $"Result<{typeof(T).Name}>: OK — {_value}" : $"Result<{typeof(T).Name}>: FAIL — {Error}";
}
