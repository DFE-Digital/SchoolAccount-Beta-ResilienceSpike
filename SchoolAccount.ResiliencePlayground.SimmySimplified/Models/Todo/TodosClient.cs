namespace SchoolAccount.ResiliencePlayground.SimmySimplified.Models.Todo;

public class TodosClient(HttpClient client)
{
    public async Task<IEnumerable<TodoModel>> GetTodosAsync(CancellationToken cancellationToken)
    {
        return await client.GetFromJsonAsync<IEnumerable<TodoModel>>("/todos", cancellationToken) ?? [];
    }
}
