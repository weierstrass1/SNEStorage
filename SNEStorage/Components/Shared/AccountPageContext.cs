using SNEStorage.Models;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Shared;

public sealed record AccountPageContext(UserInfo Profile, ResourceReadDTO? Resource);
