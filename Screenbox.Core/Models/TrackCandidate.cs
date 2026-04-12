#nullable enable

namespace Screenbox.Core.Models;

public sealed class TrackCandidate
{
    public string LanguageTag { get; set; } = string.Empty;

    public string LanguageName { get; set; } = string.Empty;

    public string LabelContains { get; set; } = string.Empty;

    public string LabelEquals { get; set; } = string.Empty;

    public int? Index { get; set; }
}
