namespace NetCheck.Core.Models;

public sealed record SettingChange(
    string SettingName,
    string PreviousValue,
    string NewValue);
