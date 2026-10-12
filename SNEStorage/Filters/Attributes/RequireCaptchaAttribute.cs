using Microsoft.AspNetCore.Mvc;

namespace SNEStorage.Filters.Attributes;

public enum CaptchaPolicy
{
    Always,
    After5TriesInShortTime,
    After10TriesInShortTime
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireCaptchaAttribute : TypeFilterAttribute
{
    public RequireCaptchaAttribute(string action, CaptchaPolicy policy = CaptchaPolicy.Always, bool allowFormToken = false) : 
        base(typeof(CaptchaFilter))
    {
        Arguments = [action, policy, allowFormToken];
    }
}
