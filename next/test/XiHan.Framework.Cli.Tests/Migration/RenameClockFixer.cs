// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XiHan.Framework.Cli.Migration;

namespace XiHan.Framework.Cli.Tests.Migration;

/// <summary>
/// 测试规则 <c>T5-01</c>。
/// </summary>
/// <remarks>
/// <c>LegacyClock</c> 改名 <c>SystemClock</c>（同名文件随之改名）并补 <c>using Shop.Time;</c>，<c>LegacyTimer</c> 类型的属性前留标记。
/// </remarks>
internal sealed class RenameClockFixer : SyntaxFixer
{
    /// <summary>
    /// 标记文案。
    /// </summary>
    public const string TimerMessage = "LegacyTimer has no replacement; rewrite it by hand.";

    /// <inheritdoc />
    public override string Id => "T5-01";

    /// <inheritdoc />
    protected override CompilationUnitSyntax Rewrite(CompilationUnitSyntax root, MigrationFile file, MigrationSession session)
    {
        var tokens = root.DescendantTokens().Where(static token => token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText == "LegacyClock").ToList();
        foreach (var token in tokens)
        {
            session.Changed(Id, file, token.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
        }

        var current = root.ReplaceTokens(tokens, static (_, token) => SyntaxFactory.Identifier(token.LeadingTrivia, "SystemClock", token.TrailingTrivia));
        if (tokens.Any(token => token.Parent is ClassDeclarationSyntax) && Path.GetFileNameWithoutExtension(file.Path) == "LegacyClock")
        {
            session.Renames[file.Path] = Path.Combine(Path.GetDirectoryName(file.Path)!, "SystemClock.cs");
        }

        var timers = current.DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .Where(property => property.Type.ToString() == "LegacyTimer" && !Markers.Has(property, Id, TimerMessage))
            .ToList();
        foreach (var timer in timers)
        {
            session.Marked(Id, file, LineOf(timer), TimerMessage);
        }

        current = current.ReplaceNodes(timers, (_, node) => Markers.With(node, Id, TimerMessage, file.NewLine));
        return tokens.Count == 0 && timers.Count == 0 ? root : WithUsings(current, ["Shop.Time"], file.NewLine);
    }
}
