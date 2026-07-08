namespace SchoolAccount.ResiliencePlayground.Models;

/// <summary>
/// A reference to a task that originated in an external service, identified by the
/// service (<see cref="Source"/>) and that service's own id (<see cref="ExternalId"/>).
/// Serialises to the composite form <c>source:externalId</c>.
/// </summary>
/// <remarks>
/// Construct via <see cref="Create"/>/<see cref="TryCreate"/> or parse via
/// <see cref="Parse"/>/<see cref="TryParse"/>. There is deliberately no public
/// constructor so every instance is validated. <c>default(TaskRef)</c> is the
/// only invalid value and is never produced by the factory methods.
/// </remarks>
/// <remarks>
/// Could do with adding DataKeysProtection with a Protect/Unprotect to ensure that with the keys being base64
/// they are spoofed.
/// </remarks>
public readonly record struct TaskReference
{
    /// <summary>The integration the task came from, e.g. <c>github</c>, <c>jira</c>.</summary>
    public string Source { get; }

    /// <summary>The id the source service uses for the task. May itself contain ':'.</summary>
    public string ExternalId { get; }

    // Trusted, validation-free constructor. All public entry points route through
    // TryCreate first, so callers can only ever obtain a validated instance.
    private TaskReference(string source, string externalId)
    {
        Source = source;
        ExternalId = externalId;
    }

    /// <summary>Validates and builds a <see cref="TaskReference"/>, or throws.</summary>
    public static TaskReference Create(string source, string externalId)
    {
        return TryCreate(source, externalId, out var value, out var error)
            ? value
            : throw new ArgumentException(error);
    }

    /// <summary>Validates and builds a <see cref="TaskReference"/> without throwing.</summary>
    public static bool TryCreate(string? source, string? externalId, out TaskReference taskReference, out string? error)
    {
        taskReference = default;
        error = null;

        if (string.IsNullOrWhiteSpace(source))
        {
            error = "Source is required.";
            return false;
        }

        if (source.Contains(':'))
        {
            error = "Source must not contain ':'.";
            return false;
        }

        if (externalId is null)
        {
            error = "ExternalId is required.";
            return false;
        }

        taskReference = new TaskReference(source, externalId);
        return true;
    }

    /// <summary>The composite string form: <c>source:externalId</c>.</summary>
    public override string ToString()
    {
        return $"{Source}:{ExternalId}";
    }

    /// <summary>Parses the composite string form, or throws.</summary>
    public static TaskReference Parse(string value)
    {
        return TryParse(value, out var taskRef)
            ? taskRef
            : throw new FormatException($"'{value}' is not a valid TaskRef; expected 'source:externalId'.");
    }

    /// <summary>Parses the composite string form without throwing.</summary>
    public static bool TryParse(string? value, out TaskReference taskReference)
    {
        taskReference = default;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // Split on the FIRST colon only, so the external id can contain colons.
        var i = value.IndexOf(':');
        return i > 0 && TryCreate(value[..i], value[(i + 1)..], out taskReference, out _);
    }
}