namespace SchoolAccount.ResiliencePlayground.Models;

public class ChaosSettings
{
    public bool LatencyEnabled { get; set; }
    public double LatencyInjectionRate { get; set; }
    public double LatencySeconds { get; set; }
    public bool FaultEnabled { get; set; }
    public double FaultInjectionRate { get; set; }
}
