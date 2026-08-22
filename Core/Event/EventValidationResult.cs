namespace Rah_Negar.Core.Event;

public sealed record EventValidationError(string Code, string Message);

public sealed class EventValidationResult
{
    private EventValidationResult(IReadOnlyList<EventValidationError> errors)
    {
        Errors = errors;
    }

    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<EventValidationError> Errors { get; }

    public static EventValidationResult Valid() =>
        new(Array.Empty<EventValidationError>());

    public static EventValidationResult Invalid(params EventValidationError[] errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Length == 0)
            throw new ArgumentException("An invalid result requires at least one error.", nameof(errors));
        return new EventValidationResult(Array.AsReadOnly(errors));
    }
}

public sealed record EventCreationResult(Event? Event, EventValidationResult Validation)
{
    public bool IsSuccess => Event is not null && Validation.IsValid;
}
