using SNEStorage.Filters.Attributes;
using SNEStorage.Services;

namespace SNEStorageTesting;

public class CaptchaFrequencyServiceTests
{
    [Fact(DisplayName = "The five-attempt policy requires a captcha after five requests")]
    public void TryAllowWithoutCaptcha_BlocksSixthAttempt_ForFiveAttemptPolicy()
    {
        using var service = new CaptchaFrequencyService();

        for (int attempt = 1; attempt <= 5; attempt++)
            Assert.True(service.TryAllowWithoutCaptcha("user-1", CaptchaPolicy.After5TriesInShortTime));

        Assert.False(service.TryAllowWithoutCaptcha("user-1", CaptchaPolicy.After5TriesInShortTime));
    }

    [Fact(DisplayName = "The ten-attempt policy requires a captcha after ten requests")]
    public void TryAllowWithoutCaptcha_BlocksEleventhAttempt_ForTenAttemptPolicy()
    {
        using var service = new CaptchaFrequencyService();

        for (int attempt = 1; attempt <= 10; attempt++)
            Assert.True(service.TryAllowWithoutCaptcha("user-1", CaptchaPolicy.After10TriesInShortTime));

        Assert.False(service.TryAllowWithoutCaptcha("user-1", CaptchaPolicy.After10TriesInShortTime));
    }

    [Fact(DisplayName = "Captcha limits are independent for each user")]
    public void TryAllowWithoutCaptcha_UsesIndependentUserPartitions()
    {
        using var service = new CaptchaFrequencyService();

        for (int attempt = 1; attempt <= 5; attempt++)
            service.TryAllowWithoutCaptcha("user-1", CaptchaPolicy.After5TriesInShortTime);

        Assert.False(service.TryAllowWithoutCaptcha("user-1", CaptchaPolicy.After5TriesInShortTime));
        Assert.True(service.TryAllowWithoutCaptcha("user-2", CaptchaPolicy.After5TriesInShortTime));
    }

    [Fact(DisplayName = "Captcha limits are independent for each policy")]
    public void TryAllowWithoutCaptcha_UsesIndependentPolicyPartitions()
    {
        using var service = new CaptchaFrequencyService();

        for (int attempt = 1; attempt <= 5; attempt++)
            service.TryAllowWithoutCaptcha("user-1", CaptchaPolicy.After5TriesInShortTime);

        Assert.False(service.TryAllowWithoutCaptcha("user-1", CaptchaPolicy.After5TriesInShortTime));
        Assert.True(service.TryAllowWithoutCaptcha("user-1", CaptchaPolicy.After10TriesInShortTime));
    }

}
