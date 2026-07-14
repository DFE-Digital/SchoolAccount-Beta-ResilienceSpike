using System.Text.Json.Serialization;

namespace SchoolAccount.ResiliencePlayground.SimmySimplified.Models.Todo;

public record TodoModel(
    [property: JsonPropertyName("id")] int Id, 
    [property: JsonPropertyName("title")] string Title);