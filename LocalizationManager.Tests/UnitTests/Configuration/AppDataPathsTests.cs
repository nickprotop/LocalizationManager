// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using LocalizationManager.Core.Configuration;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Configuration;

public class AppDataPathsTests
{
    [Fact]
    public void GetCredentialsDirectory_ReturnsValidPath()
    {
        // Act
        var directory = AppDataPaths.GetCredentialsDirectory();

        // Assert
        Assert.NotNull(directory);
        Assert.NotEmpty(directory);
        Assert.Contains("LocalizationManager", directory);
    }

    [Fact]
    public void GetCredentialsDirectory_CreatesDirectoryIfNotExists()
    {
        // Act
        var directory = AppDataPaths.GetCredentialsDirectory();

        // Assert
        Assert.True(Directory.Exists(directory));
    }

    [Fact]
    public void GetCredentialsDirectory_Windows_ContainsAppData()
    {
        // Skip if not Windows
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Act
        var directory = AppDataPaths.GetCredentialsDirectory();

        // Assert
        Assert.Contains("AppData", directory);
        Assert.Contains("Local", directory);
    }

    [Fact]
    public void GetCredentialsDirectory_Linux_ContainsLocalShare()
    {
        // Skip if not Linux
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // Act
        var directory = AppDataPaths.GetCredentialsDirectory();

        // Assert
        Assert.Contains(".local/share", directory);
    }

    [Fact]
    public void GetCredentialsFilePath_ReturnsValidPath()
    {
        // Act
        var filePath = AppDataPaths.GetCredentialsFilePath();

        // Assert
        Assert.NotNull(filePath);
        Assert.NotEmpty(filePath);
        Assert.EndsWith("credentials.json", filePath);
        Assert.Contains("LocalizationManager", filePath);
    }

    [Fact]
    public void GetCredentialsFilePath_IsAbsolutePath()
    {
        // Act
        var filePath = AppDataPaths.GetCredentialsFilePath();

        // Assert
        Assert.True(Path.IsPathRooted(filePath));
    }
}
