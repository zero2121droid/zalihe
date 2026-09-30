namespace Zalihe.Domain.Users;

/// <summary>UI languages a user can choose.</summary>
public static class Languages
{
    public const string SerbianLatin = "sr-Latn";
    public const string English = "en";
    public const string Default = SerbianLatin;
    public const int MaxLength = 10;

    public static IReadOnlyList<string> Supported { get; } = [SerbianLatin, English];

    public static bool IsSupported(string language) => Supported.Contains(language);
}
