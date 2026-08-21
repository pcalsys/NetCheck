using System.Globalization;
using NetCheck.Core.Abstractions;

namespace NetCheck.Core.Localization;

public sealed class InvariantTextLocalizer : ITextLocalizer
{
    public static InvariantTextLocalizer Instance { get; } = new();

    private InvariantTextLocalizer()
    {
    }

    public string Language => "en";

    public CultureInfo Culture => CultureInfo.GetCultureInfo("en-US");

    public string Translate(string source) => source;

    public string Format(string sourceFormat, params object?[] arguments) =>
        string.Format(Culture, sourceFormat, arguments);
}
