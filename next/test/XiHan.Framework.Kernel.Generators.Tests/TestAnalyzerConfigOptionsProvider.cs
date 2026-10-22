// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XiHan.Framework.Kernel.Generators.Tests;

/// <summary>
/// 只提供 <c>build_property.RootNamespace</c> 的分析器配置，模拟 MSBuild 的 CompilerVisibleProperty。
/// </summary>
/// <param name="rootNamespace">根命名空间。</param>
internal sealed class TestAnalyzerConfigOptionsProvider(string rootNamespace) : AnalyzerConfigOptionsProvider
{
    /// <summary>
    /// 全局选项。
    /// </summary>
    private readonly TestAnalyzerConfigOptions _global = new(rootNamespace);

    /// <inheritdoc />
    public override AnalyzerConfigOptions GlobalOptions => _global;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _global;

    /// <inheritdoc />
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _global;
}
