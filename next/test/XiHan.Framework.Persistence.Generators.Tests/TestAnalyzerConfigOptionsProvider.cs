// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace XiHan.Framework.Persistence.Generators.Tests;

/// <summary>
/// 把同一份选项用于全局与每个语法树的提供者。
/// </summary>
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
