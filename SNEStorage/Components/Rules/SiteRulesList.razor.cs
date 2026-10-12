using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Rules;

public partial class SiteRulesList
{
    [Parameter] public string Letter { get; set; } = string.Empty;
    [Parameter] public IReadOnlyList<SiteRuleEntry> Entries { get; set; } = [];
    [Parameter] public bool Gold { get; set; }
    [Parameter] public bool Grouped { get; set; }

    private int StartNumber => Entries.Count > 0 ? Entries[0].Number : 1;
}
