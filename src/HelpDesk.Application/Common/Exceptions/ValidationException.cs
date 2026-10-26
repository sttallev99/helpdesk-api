using FluentValidation.Results;

namespace HelpDesk.Application.Common.Exceptions;

/// <summary>
/// Thrown by the MediatR validation pipeline behavior when FluentValidation fails.
/// Maps to 400 with a Problem Details body listing per-field errors.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException() : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures) : this()
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }

    public IDictionary<string, string[]> Errors { get; }
}
