using SNEStorage.Models;

namespace SNEStorage.Components.Shared;

public sealed record AccountPageContext(UserInfo Profile, Resource? Resource);
