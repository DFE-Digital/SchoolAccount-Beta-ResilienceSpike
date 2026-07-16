using Polly;

namespace SchoolAccount.ResiliencePlayground.SimmySimplified.Models.Chaos;

public interface IChaosManager
{
    ValueTask<bool> IsChaosEnabledAsync(ResilienceContext context);

    ValueTask<double> GetInjectionRateAsync(ResilienceContext context);
}