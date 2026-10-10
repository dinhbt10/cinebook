namespace CineBook.Application.Common;

public class Result
{
    public bool Succeeded { get; init; }
    public string? ErrorCode { get; init; }
    public string? Error { get; init; }

    public static Result Ok() => new() { Succeeded = true };
    public static Result Fail(string code, string message) =>
        new() { Succeeded = false, ErrorCode = code, Error = message };
}

public class Result<T> : Result
{
    public T? Value { get; init; }

    public static Result<T> Ok(T value) => new() { Succeeded = true, Value = value };
    public static new Result<T> Fail(string code, string message) =>
        new() { Succeeded = false, ErrorCode = code, Error = message };
}
