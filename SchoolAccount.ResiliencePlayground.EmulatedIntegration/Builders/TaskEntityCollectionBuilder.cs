using Bogus;
using SchoolAccount.ResiliencePlayground.EmulatedIntegration.Models;
using SchoolAccount.ResiliencePlayground.Extensions;
using static SchoolAccount.ResiliencePlayground.EmulatedIntegration.Builders.TaskEntityBuilder;

namespace SchoolAccount.ResiliencePlayground.EmulatedIntegration.Builders;

public class TaskEntityCollectionBuilder
{
    private readonly Faker _faker;
    private readonly int _amount;

    protected TaskEntityCollectionBuilder(Faker? faker, int amount)
    {
        _faker = faker ?? new Faker() { Random = new Randomizer(1234) };
        _amount = amount;
    }

    public static TaskEntityCollectionBuilder ATaskCollection(Faker? faker = null, int? amount = null)
    {
        return new(faker, amount ?? 10);
    }
    
    public IEnumerable<TaskEntity> Build()
    {
        for (var i = 0; i < _amount; i++)
        {
            yield return ATask()
                .WithId(i + 1)
                .WithName(_faker.Lorem.Sentence())
                .WithDescription(_faker.Lorem.Paragraph())
                .WithDueDate(_faker.Date.Future().ToDateOnly())
                .Build();
        }
    }
}