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

namespace LocalizationManager.Shared.Enums;

/// <summary>
/// Capabilities that a plugin can support.
/// </summary>
[Flags]
public enum PluginCapabilities
{
    /// <summary>
    /// No capabilities.
    /// </summary>
    None = 0,

    /// <summary>
    /// Plugin can import data.
    /// </summary>
    Import = 1,

    /// <summary>
    /// Plugin can export data.
    /// </summary>
    Export = 2,

    /// <summary>
    /// Plugin can validate data.
    /// </summary>
    Validate = 4,

    /// <summary>
    /// Plugin can transform data.
    /// </summary>
    Transform = 8
}
