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
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backends;
using LocalizationManager.Core.Backends.Json;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Commands;

/// <summary>
/// Base settings for all commands with common options.
/// </summary>
public class BaseCommandSettings : CommandSettings
{
    private IResourceBackend? _cachedBackend;

    [CommandOption("-p|--path <PATH>")]
    [Description("Path to the Resources folder (default: current directory)")]
    public string? ResourcePath { get; set; }

    [CommandOption("--config-file <PATH>")]
    [Description("Path to configuration file (default: lrm.json in resource path)")]
    public string? ConfigFilePath { get; set; }

    [CommandOption("--backend <BACKEND>")]
    [Description("Resource backend: resx, json, i18next, android, or ios (auto-detected if not specified)")]
    public string? Backend { get; set; }

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

    /// <summary>
    /// Gets the resource backend based on settings priority:
    /// 1. --backend CLI option
    /// 2. ResourceFormat from configuration file
    /// 3. Auto-detect from files in resource path
    /// </summary>
    /// <returns>The resource backend to use (cached after first call).</returns>
    public IResourceBackend GetBackend()
    {
        if (_cachedBackend != null)
            return _cachedBackend;

        var factory = new ResourceBackendFactory();

        // Priority 1: CLI option
        if (!string.IsNullOrEmpty(Backend))
        {
            _cachedBackend = factory.GetBackend(Backend);
            return _cachedBackend;
        }

        // Priority 2: Configuration file
        if (LoadedConfiguration?.ResourceFormat != null)
        {
            _cachedBackend = factory.GetBackend(LoadedConfiguration.ResourceFormat, LoadedConfiguration);
            return _cachedBackend;
        }

        // Priority 3: Auto-detect from path
        _cachedBackend = factory.ResolveFromPath(GetResourcePath(), LoadedConfiguration);
        return _cachedBackend;
    }

    /// <summary>
    /// Gets the name of the detected/configured backend (e.g., "resx", "json").
    /// Useful for display messages.
    /// </summary>
    public string GetBackendName() => GetBackend().Name;

    /// <summary>
    /// Gets the file extension for the current backend (e.g., ".resx", ".json").
    /// </summary>
    public string GetBackendExtension() => GetBackend().SupportedExtensions.FirstOrDefault() ?? "";

    /// <summary>
    /// Discovers all language files in the resource path using the appropriate backend.
    /// </summary>
    /// <returns>List of discovered languages.</returns>
    public List<LanguageInfo> DiscoverLanguages()
    {
        return GetBackend().Discovery.DiscoverLanguages(GetResourcePath());
    }

    /// <summary>
    /// Discovers all language files asynchronously.
    /// </summary>
    public Task<List<LanguageInfo>> DiscoverLanguagesAsync(CancellationToken ct = default)
    {
        return GetBackend().Discovery.DiscoverLanguagesAsync(GetResourcePath(), ct);
    }

    /// <summary>
    /// Reads a single resource file.
    /// </summary>
    public ResourceFile ReadResourceFile(LanguageInfo language)
    {
        return GetBackend().Reader.Read(language);
    }

    /// <summary>
    /// Reads a single resource file asynchronously.
    /// </summary>
    public Task<ResourceFile> ReadResourceFileAsync(LanguageInfo language, CancellationToken ct = default)
    {
        return GetBackend().Reader.ReadAsync(language, ct);
    }

    /// <summary>
    /// Reads all resource files in the resource path.
    /// </summary>
    /// <returns>List of all resource files.</returns>
    public List<ResourceFile> ReadAllResources()
    {
        var backend = GetBackend();
        var languages = backend.Discovery.DiscoverLanguages(GetResourcePath());
        return languages.Select(l => backend.Reader.Read(l)).ToList();
    }

    /// <summary>
    /// Reads all resource files asynchronously.
    /// </summary>
    public async Task<List<ResourceFile>> ReadAllResourcesAsync(CancellationToken ct = default)
    {
        var backend = GetBackend();
        var languages = await backend.Discovery.DiscoverLanguagesAsync(GetResourcePath(), ct);
        var files = new List<ResourceFile>();
        foreach (var lang in languages)
        {
            files.Add(await backend.Reader.ReadAsync(lang, ct));
        }
        return files;
    }

    /// <summary>
    /// Writes a resource file using the appropriate backend.
    /// </summary>
    public void WriteResourceFile(ResourceFile file)
    {
        GetBackend().Writer.Write(file);
    }

    /// <summary>
    /// Writes a resource file asynchronously.
    /// </summary>
    public Task WriteResourceFileAsync(ResourceFile file, CancellationToken ct = default)
    {
        return GetBackend().Writer.WriteAsync(file, ct);
    }
}
