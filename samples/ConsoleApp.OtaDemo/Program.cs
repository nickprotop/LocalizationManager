// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

// =============================================================================
// LRM OTA (Over-The-Air) Localization Demo
// =============================================================================
//
// This sample demonstrates OTA localization using a mock HTTP handler that
// simulates the LRM Cloud API. No real server connection is required!
//
// Features demonstrated:
// 1. Initial OTA bundle fetch
// 2. ETag caching (304 Not Modified)
// 3. Culture/language switching
// 4. Pluralization with CLDR forms
// 5. Live translation updates
// 6. Fallback to embedded resources when offline
// 7. Circuit breaker recovery
//
// =============================================================================

using System.Globalization;
using ConsoleApp.OtaDemo;
using LocalizationManager.JsonLocalization;
using LocalizationManager.JsonLocalization.Ota;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          LRM OTA (Over-The-Air) Localization Demo            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// -----------------------------------------------------------------------------
// STEP 1: Initialize OTA with Mock Handler
// -----------------------------------------------------------------------------

Console.WriteLine("[1] Initializing OTA client...");
Console.WriteLine("    Endpoint: https://mock-lrm-cloud.local (simulated)");
Console.WriteLine("    Project:  @demo/sample-app");
Console.WriteLine();

// Create mock handler that simulates LRM Cloud
var mockHandler = new MockOtaHandler();
var httpClient = new HttpClient(mockHandler);

// Configure OTA options
var otaOptions = new OtaOptions
{
    Endpoint = "https://mock-lrm-cloud.local",
    ApiKey = "lrm_demo_mock_api_key",
    Project = "@demo/sample-app",
    RefreshInterval = TimeSpan.FromSeconds(30),
    FallbackToLocal = true
};

// Create OTA client with our mock handler
var otaClient = new OtaClient(otaOptions, httpClient);

// Create fallback loader for embedded resources
var fallbackLoader = new EmbeddedResourceLoader(
    typeof(Program).Assembly,
    "ConsoleApp.OtaDemo.Resources"
);

// Create OTA resource loader that uses OTA client + fallback
var otaLoader = new OtaResourceLoader(otaClient, fallbackLoader);

// Create localizer
var localizer = new JsonLocalizer(otaLoader, "strings");

// -----------------------------------------------------------------------------
// STEP 2: Initial Bundle Fetch
// -----------------------------------------------------------------------------

Console.WriteLine("[2] Fetching initial bundle from OTA (simulated)...");
await otaClient.RefreshAsync(force: true);

var bundle = otaClient.CachedBundle;
if (bundle != null)
{
    Console.WriteLine($"    [OK] Bundle loaded successfully!");
    Console.WriteLine($"    Version:   {bundle.Version}");
    Console.WriteLine($"    Languages: {string.Join(", ", bundle.Languages)}");
    Console.WriteLine($"    Keys:      {bundle.Translations.Values.Sum(l => l.Count)} total");
}
Console.WriteLine();

// -----------------------------------------------------------------------------
// STEP 3: Basic Translations (English)
// -----------------------------------------------------------------------------

Console.WriteLine("[3] Basic translations (English - default):");
localizer.Culture = new CultureInfo("en");

Console.WriteLine($"    Welcome:  {localizer["Welcome"]}");
Console.WriteLine($"    Goodbye:  {localizer["Goodbye"]}");
Console.WriteLine($"    AppTitle: {localizer["AppTitle"]}");
Console.WriteLine($"    Greeting: {localizer["Greeting", "World"]}");
Console.WriteLine();

// -----------------------------------------------------------------------------
// STEP 4: Culture Switching
// -----------------------------------------------------------------------------

Console.WriteLine("[4] Switching to French (fr)...");
localizer.Culture = new CultureInfo("fr");

Console.WriteLine($"    Welcome:  {localizer["Welcome"]}");
Console.WriteLine($"    Goodbye:  {localizer["Goodbye"]}");
Console.WriteLine($"    Greeting: {localizer["Greeting", "Monde"]}");
Console.WriteLine();

Console.WriteLine("    Switching to German (de)...");
localizer.Culture = new CultureInfo("de");

Console.WriteLine($"    Welcome:  {localizer["Welcome"]}");
Console.WriteLine($"    Goodbye:  {localizer["Goodbye"]}");
Console.WriteLine($"    Greeting: {localizer["Greeting", "Welt"]}");
Console.WriteLine();

// Reset to English for remaining demos
localizer.Culture = new CultureInfo("en");

// -----------------------------------------------------------------------------
// STEP 5: Pluralization Demo
// -----------------------------------------------------------------------------

Console.WriteLine("[5] Pluralization demo (CLDR forms):");

Console.WriteLine("    Items:");
Console.WriteLine($"      0: {localizer.Plural("Items", 0)}");
Console.WriteLine($"      1: {localizer.Plural("Items", 1)}");
Console.WriteLine($"      5: {localizer.Plural("Items", 5)}");

Console.WriteLine("    Messages:");
Console.WriteLine($"      0: {localizer.Plural("Messages", 0)}");
Console.WriteLine($"      1: {localizer.Plural("Messages", 1)}");
Console.WriteLine($"      42: {localizer.Plural("Messages", 42)}");
Console.WriteLine();

// -----------------------------------------------------------------------------
// STEP 6: Simulate Live Update
// -----------------------------------------------------------------------------

Console.WriteLine("[6] Simulating live translation update...");
Console.WriteLine($"    Before: {localizer["Welcome"]}");

// Simulate someone updating the translation via LRM Cloud web UI
mockHandler.SimulateUpdate("en", "Welcome", "Hello & Welcome to the NEW LRM!");
Console.WriteLine("    --- Cloud update: 'Welcome' changed ---");

// Force refresh to get new bundle
await otaClient.RefreshAsync(force: true);

Console.WriteLine($"    After:  {localizer["Welcome"]}");
Console.WriteLine($"    New version: {otaClient.CachedBundle?.Version}");
Console.WriteLine();

// -----------------------------------------------------------------------------
// STEP 7: ETag Caching Demo
// -----------------------------------------------------------------------------

Console.WriteLine("[7] ETag caching demo (efficient polling):");
var requestsBefore = mockHandler.RequestCount;

// Try to refresh - should get 304 Not Modified
var updated = await otaClient.RefreshAsync(force: true);
Console.WriteLine($"    First refresh: {(updated ? "Bundle updated" : "Not modified (304)")}");

// Second refresh - still no change
updated = await otaClient.RefreshAsync(force: true);
Console.WriteLine($"    Second refresh: {(updated ? "Bundle updated" : "Not modified (304)")}");

// Now make a change
mockHandler.SimulateUpdate("en", "Goodbye", "See you later!");
updated = await otaClient.RefreshAsync(force: true);
Console.WriteLine($"    After change: {(updated ? "Bundle updated" : "Not modified (304)")}");

Console.WriteLine($"    Total HTTP requests: {mockHandler.RequestCount - requestsBefore}");
Console.WriteLine();

// -----------------------------------------------------------------------------
// STEP 8: Fallback Demo (Offline Mode)
// -----------------------------------------------------------------------------

Console.WriteLine("[8] Fallback demo (simulating offline mode)...");
Console.WriteLine($"    Current (OTA): {localizer["Welcome"]}");

// Enable offline simulation
mockHandler.SimulateOffline = true;
Console.WriteLine("    --- Network disconnected (simulated) ---");

// Try to refresh - will fail
try
{
    await otaClient.RefreshAsync(force: true);
}
catch (Exception ex)
{
    Console.WriteLine($"    [!] OTA fetch failed: {ex.Message.Split('-')[0].Trim()}");
}

// Key that only exists in fallback resources
Console.WriteLine($"    FallbackOnly key: {localizer["FallbackOnly"]}");
Console.WriteLine("    (This key only exists in embedded resources, not OTA)");

// Re-enable online mode
mockHandler.SimulateOffline = false;
Console.WriteLine("    --- Network restored ---");
Console.WriteLine();

// -----------------------------------------------------------------------------
// STEP 9: Summary
// -----------------------------------------------------------------------------

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                      Demo Complete!                          ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine("║ Features demonstrated:                                       ║");
Console.WriteLine("║   [OK] OTA bundle fetching with mock HTTP handler            ║");
Console.WriteLine("║   [OK] ETag caching (304 Not Modified responses)             ║");
Console.WriteLine("║   [OK] Multi-language support (en, fr, de)                   ║");
Console.WriteLine("║   [OK] CLDR pluralization (zero, one, other forms)           ║");
Console.WriteLine("║   [OK] Live translation updates                              ║");
Console.WriteLine("║   [OK] Fallback to embedded resources                        ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║ To use with real LRM Cloud:                                  ║");
Console.WriteLine("║   1. Replace MockOtaHandler with real HttpClient             ║");
Console.WriteLine("║   2. Set endpoint to https://lrm-cloud.com                   ║");
Console.WriteLine("║   3. Use your actual API key and project path                ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
