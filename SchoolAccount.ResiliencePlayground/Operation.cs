namespace SchoolAccount.ResiliencePlayground;

public class Operation
{
    public bool Success => Error is null;
    
    public Error? Error { get; init; }
    
    public static Operation Ok() => new();
    public static Operation<T> Ok<T>(T value) => new() { Value = value };
    public static Operation Failed(Error error) => new() { Error = error };
    public static Operation<T> Failed<T>(Error error) => new() { Error = error };
    public static Operation Failed(string message) => new() { Error = new Error(message) };
    public static Operation<T> Failed<T>(string message) => new() { Error = new Error(message) };
    public static Operation Exception(Exception ex) => new() { Error = new Error(ex.Message) };
    public static Operation<T> Exception<T>(Exception ex) => new() { Error = new Error(ex.Message) };
}

public record Error(string Message);

public class Operation<T> : Operation
{
    public T? Value { get; init; }
}