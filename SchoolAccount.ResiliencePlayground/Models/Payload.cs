namespace SchoolAccount.ResiliencePlayground.Models;

public class Payload<T> where T : class
{
    public PayloadService Service { get; init; } = null!;
    public PayloadHealth Health { get; init; } = null!;
    public List<IdentifyingRow<T>> Rows { get; init; } = [];
}

public class IdentifyingRow<T> where T : class
{
    public TaskReference Id { get; init; }
    public T Data { get; init; } = null!;
}

public class PayloadService
{
    public string Name { get; init; } = null!;
    public string QueryPath { get; init; } = null!;
}

public class PayloadHealth
{
    public TimeSpan ResponseTime { get; init; }
    public int StatusCode { get; init; }
    public double Score { get; init; }
}