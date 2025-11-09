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
using LocalizationManager.Core.Enums;

namespace LocalizationManager.Commands;

/// <summary>
/// Base settings for commands that support formatted output.
/// </summary>
public class BaseFormattableCommandSettings : BaseCommandSettings
{
    [CommandOption("-f|--format <FORMAT>")]
    [Description("Output format: table (default), json, or simple")]
    [DefaultValue("table")]
    public string Format { get; set; } = "table";

    /// <summary>
    /// Gets the parsed output format.
    /// </summary>
    public OutputFormat GetOutputFormat()
    {
        return Core.Output.OutputFormatter.ParseFormat(Format, OutputFormat.Table);
    }
}
