namespace FinaryExport.Models;

public sealed record FinaryResponse<T>
{
    public required T? Result { get; init; }
    public string? Message { get; init; }
    public FinaryError? Error { get; init; }
}

public sealed record FinaryError
{
    public string? Code { get; init; }
    public string? Message { get; init; }
}
