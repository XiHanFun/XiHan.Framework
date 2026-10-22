// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XiHan.Framework.Kernel.Generators.Tests;

/// <summary>
/// 只有一个键的分析器配置。
/// </summary>
/// <param name="rootNamespace">根命名空间。</param>
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
