namespace SchoolAccount.ResiliencePlayground.EmulatedIntegration.Models;

public record TaskEntity
{
    public int Key { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly? DueDate { get; set; }
}