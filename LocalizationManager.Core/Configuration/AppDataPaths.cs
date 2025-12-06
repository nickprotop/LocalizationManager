// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace LocalizationManager.Core.Configuration;

/// <summary>
/// Provides cross-platform paths for application data storage.
/// </summary>
public static class AppDataPaths
{
    private const string ApplicationName = "LocalizationManager";
    private const string CredentialsFileName = "credentials.json";

    /// <summary>
    /// Gets the application data directory for storing user-specific data.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    /// <returns>
    /// Windows: C:\Users\&lt;user&gt;\AppData\Local\LocalizationManager
    /// Linux:   /home/&lt;user&gt;/.local/share/LocalizationManager
    /// macOS:   /Users/&lt;user&gt;/.local/share/LocalizationManager
    /// </returns>
    public static string GetCredentialsDirectory()
    {
        var appData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);

        var appDir = Path.Combine(appData, ApplicationName);

        // Create directory if it doesn't exist
        if (!Directory.Exists(appDir))
        {
            Directory.CreateDirectory(appDir);
        }

        return appDir;
    }

    /// <summary>
    /// Gets the full path to the credentials file where encrypted API keys are stored.
    /// </summary>
    /// <returns>
    /// Windows: C:\Users\&lt;user&gt;\AppData\Local\LocalizationManager\credentials.json
    /// Linux:   /home/&lt;user&gt;/.local/share/LocalizationManager/credentials.json
    /// macOS:   /Users/&lt;user&gt;/.local/share/LocalizationManager/credentials.json
    /// </returns>
    public static string GetCredentialsFilePath()
    {
        return Path.Combine(GetCredentialsDirectory(), CredentialsFileName);
    }
}
