// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XiHan.Framework.Persistence.Generators.Tests;

/// <summary>
/// 只提供 <c>build_property.RootNamespace</c> 的分析器选项。
/// </summary>
internal sealed class TestAnalyzerConfigOptions(string rootNamespace) : AnalyzerConfigOptions
{
    /// <inheritdoc />
    public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        if (key == "build_property.RootNamespace")
        {
            value = rootNamespace;
            return true;
        }

        value = null;
        return false;
    }
}
