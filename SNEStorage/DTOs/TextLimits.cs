namespace SNEStorage.DTOs;

public static class TextLimits
{
    public const int TAG = 20;
    public const int EXTERNAL_AUTHOR = 200;
    public const int USERNAME = 256;
    public const int EMAIL = 256;
    public const int PASSWORD = 128;
    // A larger login allowance preserves older passwords above the new creation limit.
    public const int LOGIN_PASSWORD = 4096;
    public const int SEARCH = 256;
    public const int RESOURCE_NAME_SEARCH = 100;
    public const int USER_ID = 450;
    public const int ROLE = 64;
    public const int TOKEN = 4096;
    public const int RETURN_URL = 2048;
    public const int OAUTH_CODE = 4096;
    public const int OAUTH_STATE = 43;
}
