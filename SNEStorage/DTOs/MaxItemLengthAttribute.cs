using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace SNEStorage.DTOs;

// MaxLength on an array limits its count; this attribute limits each string.
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MaxItemLengthAttribute(int maximumLength) : ValidationAttribute
{
    public int MaximumLength { get; } = maximumLength;

    public override bool IsValid(object? value) => value is null ||
        value is IEnumerable<string?> items && items.All(item =>
            item is not null && item.Length <= MaximumLength);

    public override string FormatErrorMessage(string name) => string.Format(
        CultureInfo.CurrentCulture,
        ErrorMessage ?? "Each entry in {0} must be a string of at most {1} characters.",
        name, MaximumLength);
}
