using SchoolAccount.ResiliencePlayground.EmulatedIntegration.Models;

namespace SchoolAccount.ResiliencePlayground.EmulatedIntegration.Builders;

public class TaskEntityBuilder
{
    private int _id;
    private string _name;
    private string? _description;
    private DateOnly? _dueDate;
    
    protected TaskEntityBuilder()
    { }

    public TaskEntityBuilder WithId(int id)
    {
        _id = id;
        return this;
    }
    
    public TaskEntityBuilder WithName(string name)
    {
        _name = name;
        return this;
    }
    
    public TaskEntityBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public TaskEntityBuilder WithDueDate(DateOnly? dueDate)
    {
        _dueDate = dueDate;
        return this;
    }

    public static TaskEntityBuilder ATask()
    {
        return new();
    }

    public TaskEntity Build()
    {
        return new TaskEntity()
        {
            Key = _id,
            Name = _name,
            Description = _description,
            DueDate = _dueDate
        };
    }
}