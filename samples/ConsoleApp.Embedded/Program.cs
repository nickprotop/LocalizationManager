// Sample: Console App with Embedded Resources
// This demonstrates using JsonLocalizer with resources embedded in the assembly.

using System.Globalization;
using System.Reflection;
using LocalizationManager.JsonLocalization;

Console.WriteLine("=== JsonLocalizer Embedded Resources Demo ===\n");

// Create localizer using embedded resources
// The resources are embedded with namespace prefix: ConsoleApp.Embedded.Resources
var assembly = Assembly.GetExecutingAssembly();
using var localizer = new JsonLocalizer(
    assembly,
    resourceNamespace: "ConsoleApp.Embedded.Resources",
    baseName: "strings");

// Display available cultures
Console.WriteLine("Available cultures from embedded resources:");
foreach (var culture in localizer.AvailableCultures)
{
    Console.WriteLine($"  - {(string.IsNullOrEmpty(culture) ? "(default)" : culture)}");
}
Console.WriteLine();

// Demo 1: Default culture
Console.WriteLine("--- Default Culture (English) ---");
Console.WriteLine($"appName: {localizer["appName"]}");
Console.WriteLine($"greeting: {localizer["greeting", "Developer"]}");
Console.WriteLine($"buttons.ok: {localizer["buttons.ok"]}");
Console.WriteLine($"errors.notFound: {localizer["errors.notFound", "config.json"]}");
Console.WriteLine();

// Demo 2: Spanish
Console.WriteLine("--- Spanish (es) ---");
localizer.Culture = new CultureInfo("es");
Console.WriteLine($"appName: {localizer["appName"]}");
Console.WriteLine($"greeting: {localizer["greeting", "Desarrollador"]}");
Console.WriteLine($"buttons.ok: {localizer["buttons.ok"]}");
Console.WriteLine($"errors.notFound: {localizer["errors.notFound", "config.json"]}");
Console.WriteLine();

// Demo 3: Japanese
Console.WriteLine("--- Japanese (ja) ---");
localizer.Culture = new CultureInfo("ja");
Console.WriteLine($"appName: {localizer["appName"]}");
Console.WriteLine($"greeting: {localizer["greeting", "開発者"]}");
Console.WriteLine($"buttons.ok: {localizer["buttons.ok"]}");
Console.WriteLine($"errors.notFound: {localizer["errors.notFound", "config.json"]}");
Console.WriteLine();

// Demo 4: Pluralization in different cultures
Console.WriteLine("--- Pluralization ---");
foreach (var cultureName in new[] { "", "es", "ja" })
{
    localizer.Culture = string.IsNullOrEmpty(cultureName)
        ? CultureInfo.InvariantCulture
        : new CultureInfo(cultureName);

    var label = string.IsNullOrEmpty(cultureName) ? "English" : cultureName;
    Console.WriteLine($"{label}:");
    Console.WriteLine($"  0: {localizer.Plural("fileCount", 0)}");
    Console.WriteLine($"  1: {localizer.Plural("fileCount", 1)}");
    Console.WriteLine($"  5: {localizer.Plural("fileCount", 5)}");
}
Console.WriteLine();

Console.WriteLine("=== Demo Complete ===");
Console.WriteLine();
Console.WriteLine("Notice: Resources are embedded in the assembly - no external files needed!");
