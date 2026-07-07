namespace SchoolAccount.ResiliencePlayground.EmulatedIntegration.Models;

public class EnvironmentSettings
{
    public static string SectionName => "Environment";
    
    public required string Name { get; init; }
    public double ErrorRate { get; init; } = 0d;
    public double SlowRate { get; init; } = 0d;
    public int SlowDelayMs { get; init; } = 0;
}