using System;
using System.Linq;
using System.Reflection;

public static class AssemblyInfo
{
    private static readonly Assembly _assembly = typeof(AssemblyInfo).Assembly;

    public static string Title => GetAttributeOrDefault("Title", "Untitled");
    public static Version Version => _assembly.GetName().Version;
    public static string Description => GetAttributeOrDefault("Description", "No description available.");
    public static string Product => GetAttributeOrDefault("Product", "No product name specified.");
    public static string Copyright => GetAttributeOrDefault("Copyright", "© No copyright specified.");
    public static string Trademark => GetAttributeOrDefault("Trademark", "No trademark specified.");
    public static string Company => GetAttributeOrDefault("Company", "No company specified.");

    private static string GetAttributeOrDefault(string attributeName, string defaultValue)
    {
        var attribute = _assembly.GetCustomAttributes(typeof(Attribute), false).FirstOrDefault(attr => attr.GetType().Name == attributeName);
        return attribute != null ? attribute.ToString() : defaultValue;
    }
}
