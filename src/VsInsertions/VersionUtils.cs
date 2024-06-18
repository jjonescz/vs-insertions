using System.Reflection;

static class VersionUtils
{
    public static string? GetVersion()
    {
        return typeof(VersionUtils).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
    }
}
