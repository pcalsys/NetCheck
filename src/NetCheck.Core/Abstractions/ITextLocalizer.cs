using System.Globalization;

namespace NetCheck.Core.Abstractions;

public interface ITextLocalizer
{
    string Language { get; }

    CultureInfo Culture { get; }

    string Translate(string source);

    string Format(string sourceFormat, params object?[] arguments);
}
