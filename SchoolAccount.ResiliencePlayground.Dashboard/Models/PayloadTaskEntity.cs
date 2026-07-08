namespace SchoolAccount.ResiliencePlayground.Dashboard.Models;

public record PayloadTaskEntity
{
    public int Key { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly? DueDate { get; set; }
}