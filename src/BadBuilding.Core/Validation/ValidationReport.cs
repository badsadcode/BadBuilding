namespace BadBuilding.Validation;

public enum Severity { Info, Warn, Error }

/// <summary>One finding: a short stable code ("footprint.too_small"), a message saying why, and what was done about it.</summary>
public sealed record ValidationIssue(Severity Severity, string Code, string Message, string? Fallback = null);

/// <summary>Pass, warn or error (the worst issue wins), with the issues and a few metrics. After BadShip's report.</summary>
public sealed class ValidationReport
{
    public List<ValidationIssue> Issues { get; } = [];
    public Dictionary<string, double> Metrics { get; } = new(StringComparer.Ordinal);

    public string Status => Issues.Any(i => i.Severity == Severity.Error) ? "error" : Issues.Any(i => i.Severity == Severity.Warn) ? "warn" : "pass";
    public bool HasErrors => Issues.Any(i => i.Severity == Severity.Error);

    public void Add(Severity s, string code, string message, string? fallback = null) => Issues.Add(new(s, code, message, fallback));
}
