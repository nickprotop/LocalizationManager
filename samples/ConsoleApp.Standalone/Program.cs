// Sample: Console App with Standalone JsonLocalizer
// This demonstrates using JsonLocalizer directly without dependency injection.

using System.Globalization;
using LocalizationManager.JsonLocalization;

Console.WriteLine("=== JsonLocalizer Standalone Demo ===\n");

// Create localizer pointing to Resources folder
var resourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources");
using var localizer = new JsonLocalizer(resourcesPath, "strings");

// Display available cultures
Console.WriteLine("Available cultures:");
foreach (var culture in localizer.AvailableCultures)
{
    Console.WriteLine($"  - {(string.IsNullOrEmpty(culture) ? "(default)" : culture)}");
}
Console.WriteLine();

// Demo 1: Basic string access (default culture)
Console.WriteLine("--- Default Culture (CurrentUICulture) ---");
Console.WriteLine($"welcome: {localizer["welcome"]}");
Console.WriteLine($"greeting: {localizer["greeting", "World"]}");
Console.WriteLine($"navigation.home: {localizer["navigation.home"]}");
Console.WriteLine();

// Demo 2: Switch to French
Console.WriteLine("--- French (fr) ---");
localizer.Culture = new CultureInfo("fr");
Console.WriteLine($"welcome: {localizer["welcome"]}");
Console.WriteLine($"greeting: {localizer["greeting", "Monde"]}");
Console.WriteLine($"navigation.home: {localizer["navigation.home"]}");
Console.WriteLine();

// Demo 3: Switch to German
Console.WriteLine("--- German (de) ---");
localizer.Culture = new CultureInfo("de");
Console.WriteLine($"welcome: {localizer["welcome"]}");
Console.WriteLine($"greeting: {localizer["greeting", "Welt"]}");
Console.WriteLine($"navigation.home: {localizer["navigation.home"]}");
Console.WriteLine();

// Demo 4: Pluralization
Console.WriteLine("--- Pluralization (English) ---");
localizer.Culture = new CultureInfo("en");
Console.WriteLine($"0 items: {localizer.Plural("items", 0)}");
Console.WriteLine($"1 item:  {localizer.Plural("items", 1)}");
Console.WriteLine($"5 items: {localizer.Plural("items", 5)}");
Console.WriteLine();

// Demo 5: All strings enumeration
Console.WriteLine("--- All Strings (French) ---");
localizer.Culture = new CultureInfo("fr");
foreach (var kvp in localizer.GetAllStrings().Take(5))
{
    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
}
Console.WriteLine("  ...");
Console.WriteLine();

Console.WriteLine("=== Demo Complete ===");
