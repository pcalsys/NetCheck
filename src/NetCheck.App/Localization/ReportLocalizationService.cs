using NetCheck.Core.Models;

namespace NetCheck.App.Localization;

public sealed class ReportLocalizationService(LocalizationService text)
{
    private readonly LocalizationService _text = text ?? throw new ArgumentNullException(nameof(text));

    public DiagnosticReport Localize(DiagnosticReport source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!_text.IsGerman)
        {
            return source;
        }

        return source with
        {
            Checks = source.Checks.Select(Localize).ToArray(),
            Diagnosis = source.Diagnosis with
            {
                Headline = _text.Translate(source.Diagnosis.Headline),
                Summary = _text.Translate(source.Diagnosis.Summary),
                RecommendedActions = source.Diagnosis.RecommendedActions.Select(_text.Translate).ToArray()
            }
        };
    }

    public DiagnosticCheckResult Localize(DiagnosticCheckResult source) => source with
    {
        Title = _text.Translate(source.Title),
        Summary = _text.Translate(source.Summary),
        Detail = _text.Translate(source.Detail),
        Evidence = source.Evidence.ToDictionary(
            item => _text.Translate(item.Key),
            item => _text.Translate(item.Value),
            StringComparer.OrdinalIgnoreCase),
        Recommendations = source.Recommendations.Select(_text.Translate).ToArray()
    };

    public NetworkRepairPlan Localize(NetworkRepairPlan source) => source with
    {
        Actions = source.Actions.Select(action => action with
        {
            Title = _text.Translate(action.Title),
            Description = _text.Translate(action.Description)
        }).ToArray(),
        ManualGuidance = source.ManualGuidance.Select(_text.Translate).ToArray()
    };

    public NetworkRepairResult Localize(NetworkRepairResult source) => source with
    {
        Steps = source.Steps.Select(step => step with
        {
            Title = _text.Translate(step.Title),
            Summary = _text.Translate(step.Summary),
            Detail = _text.Translate(step.Detail)
        }).ToArray()
    };
}
