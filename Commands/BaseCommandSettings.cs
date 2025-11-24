// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Spectre.Console.Cli;
using System.ComponentModel;
using LocalizationManager.Core.Configuration;

namespace LocalizationManager.Commands;

/// <summary>
/// Base settings for all commands with common options.
/// </summary>
public class BaseCommandSettings : CommandSettings
{
    [CommandOption("-p|--path <PATH>")]
    [Description("Path to the Resources folder (default: current directory)")]
    public string? ResourcePath { get; set; }

    [CommandOption("--config-file <PATH>")]
    [Description("Path to configuration file (default: lrm.json in resource path)")]
    public string? ConfigFilePath { get; set; }

    /// <summary>
    /// Gets the loaded configuration, if any.
    /// </summary>
    public ConfigurationModel? LoadedConfiguration { get; private set; }

    /// <summary>
    /// Gets the path from which the configuration was loaded, if any.
    /// </summary>
    public string? LoadedConfigurationPath { get; private set; }

    /// <summary>
    /// Gets the resource path, defaulting to current directory if not specified.
    /// </summary>
    public string GetResourcePath()
    {
        return ResourcePath ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Loads configuration from file if available.
    /// Should be called early in command execution.
    /// </summary>
    public void LoadConfiguration()
    {
        var (config, loadedFrom) = Core.Configuration.ConfigurationManager.LoadConfiguration(
            ConfigFilePath,
            GetResourcePath());

        LoadedConfiguration = config;
        LoadedConfigurationPath = loadedFrom;
    }
}
