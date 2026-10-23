// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-10（源码）：删除指向 4.x 命名空间（5.0 里不存在的 <c>XiHan.Framework.*</c>）的 <c>using</c>。
/// </summary>
internal sealed class LegacyUsingFixer : SyntaxFixer
{
    /// <inheritdoc />
    public override string Id => "V4-10";

    /// <inheritdoc />
    protected override CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session)
    {
        var legacy = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Where(static u => u.Name is not null && u.Alias is null && !V4Packages.IsNextNamespace(u.Name.ToString()))
            .ToList();
        if (legacy.Count == 0)
        {
            return root;
        }

        foreach (var directive in legacy)
        {
            session.Changed(Id, file, LineOf(directive));
        }

        // 连同各自的尾随换行一起删掉，不留空行
        return root.RemoveNodes(legacy, SyntaxRemoveOptions.KeepLeadingTrivia | SyntaxRemoveOptions.KeepUnbalancedDirectives)!;
    }
}
