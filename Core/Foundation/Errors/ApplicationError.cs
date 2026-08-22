namespace Rah_Negar.Foundation.Errors;

public sealed record ApplicationError(string Code, string Message, string? Detail = null)
{
    public static ApplicationError Create(string code, string message, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new ApplicationError(code, message, detail);
    }
}
