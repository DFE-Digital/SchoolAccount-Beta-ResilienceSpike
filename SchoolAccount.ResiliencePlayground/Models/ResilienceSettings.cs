namespace SchoolAccount.ResiliencePlayground.Models;

public class ResilienceSettings
{
    public int? MaxRetryAttempts { get; set; }
    public double? DelaySeconds { get; set; }
    public double? TimeoutSeconds { get; set; }
    public int? CircuitBreakAfterFailures { get; set; }
    public double? DegradedResponseThresholdSeconds { get; set; }
}