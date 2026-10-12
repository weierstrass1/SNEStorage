using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using SNEStorage.DTOs;

namespace SNEStorage.Components.News;

public partial class NewsCard
{
    private static readonly Regex HexColor = new("\\A#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\z", RegexOptions.CultureInvariant);
    private string? _failedAvatar;
    private bool _editing, _saved;
    [Parameter, EditorRequired] public NewsReadDTO Item { get; set; } = default!;
    [Parameter] public bool Compact { get; set; }
    [Parameter] public bool CanEdit { get; set; }
    private string ResolvedAvatar => PublicFileUrl.Resolve(Item.UserSummary.Avatar?.URL, "/images/caja.png");
    private string AvatarUrl => _failedAvatar == ResolvedAvatar ? "/images/caja.png" : ResolvedAvatar;
    private string ProfileUrl => $"/Profile/{Uri.EscapeDataString(Item.UserSummary.Username)}";
    private string UsernameColor => Item.UserSummary.UsernameColor is { } color && HexColor.IsMatch(color) ?
        color : "#dec4fa";
    private string PublicationDate => Item.CreatedAt.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    private void ShowFallbackAvatar() => _failedAvatar = ResolvedAvatar;

    protected override void OnParametersSet()
    {
        if (!CanEdit) _editing = false;
    }

    private void StartEditing() { if (CanEdit) { _editing = true; _saved = false; } }
    private void CancelEditing() => _editing = false;
    private void NewsSaved(NewsEditDTO dto)
    {
        Item.Title = dto.Title ?? Item.Title;
        Item.Text = dto.Text ?? Item.Text;
        _editing = false;
        _saved = true;
    }
}
