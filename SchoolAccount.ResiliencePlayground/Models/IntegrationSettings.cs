using System.Collections.Generic;

namespace SchoolAccount.ResiliencePlayground.Models;

public class IntegrationSettings
{
    public const string SectionName = "Integrations";
    public ResilienceSettings Defaults { get; set; } = new();
    public List<ServiceManifest> Services { get; set; } = [];
}