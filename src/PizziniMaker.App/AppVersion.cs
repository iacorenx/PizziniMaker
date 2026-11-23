using PizziniMaker.Core;

namespace PizziniMaker.App;

public static class AppVersion
{
    public static Version Current { get; } =
        UpdateChecker.Normalize(typeof(AppVersion).Assembly.GetName().Version ?? new Version(0, 0, 0));

    public static string Display => Current.ToString(3);

    public const string Credits = "Energy Srl · Iacopo Preci · 2026";
}
