// Sample: Console App with Source Generator
// This demonstrates using the source generator for strongly-typed resource access.
//
// The generator reads JSON files at compile time and generates a static class
// with properties for each key, providing compile-time safety and IntelliSense.

using System.Globalization;

Console.WriteLine("=== Source Generator Demo ===\n");

// Initialize the generated Strings class with the resources path
var resourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources");
ConsoleApp.SourceGenerator.Strings.Initialize(resourcesPath);

// Access localized strings with compile-time type safety
Console.WriteLine("--- Strongly-Typed Access (Default Culture) ---");
Console.WriteLine($"AppTitle: {ConsoleApp.SourceGenerator.Strings.AppTitle}");
Console.WriteLine($"Welcome: {ConsoleApp.SourceGenerator.Strings.Welcome}");
Console.WriteLine();

// Nested keys become nested classes
Console.WriteLine("--- Nested Keys ---");
Console.WriteLine($"Buttons.Save: {ConsoleApp.SourceGenerator.Strings.Buttons.Save}");
Console.WriteLine($"Buttons.Cancel: {ConsoleApp.SourceGenerator.Strings.Buttons.Cancel}");
Console.WriteLine($"Errors.Required: {ConsoleApp.SourceGenerator.Strings.Errors.Required}");
Console.WriteLine();

// Switch culture
Console.WriteLine("--- French (fr) ---");
ConsoleApp.SourceGenerator.Strings.Localizer.Culture = new CultureInfo("fr");
Console.WriteLine($"AppTitle: {ConsoleApp.SourceGenerator.Strings.AppTitle}");
Console.WriteLine($"Welcome: {ConsoleApp.SourceGenerator.Strings.Welcome}");
Console.WriteLine($"Buttons.Save: {ConsoleApp.SourceGenerator.Strings.Buttons.Save}");
Console.WriteLine();

// Pluralization
Console.WriteLine("--- Pluralization (English) ---");
ConsoleApp.SourceGenerator.Strings.Localizer.Culture = new CultureInfo("en");
Console.WriteLine($"0: {ConsoleApp.SourceGenerator.Strings.ItemCount(0)}");
Console.WriteLine($"1: {ConsoleApp.SourceGenerator.Strings.ItemCount(1)}");
Console.WriteLine($"5: {ConsoleApp.SourceGenerator.Strings.ItemCount(5)}");
Console.WriteLine();

Console.WriteLine("=== Demo Complete ===");
Console.WriteLine();
Console.WriteLine("Note: All resource keys are compile-time checked!");
Console.WriteLine("Try renaming a key in strings.json and rebuild - you'll get a compile error.");
