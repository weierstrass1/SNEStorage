using Microsoft.JSInterop;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.News;

// A feed shares its browser module/session monitor; cards retain their own vote state.
internal sealed record NewsFeedContext(IJSObjectReference Module, VerifiedAccountContent.BrowserAccount? Account);
