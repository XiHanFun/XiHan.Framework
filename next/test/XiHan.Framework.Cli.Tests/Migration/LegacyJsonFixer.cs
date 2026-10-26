// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Migration;

namespace XiHan.Framework.Cli.Tests.Migration;

/// <summary>
/// 测试规则 <c>T5-03</c>：<c>*.legacy.json</c> 转成去掉 <c>.legacy</c> 的同名文件，原文件删除。
/// </summary>
internal sealed class LegacyJsonFixer : MigrationFixer
{
    /// <inheritdoc />
    public override string Id => "T5-03";

    /// <inheritdoc />
    public override bool Handles(MigrationFile file) => file.Path.EndsWith(".legacy.json", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override string Apply(MigrationFile file, string text, MigrationSession session)
    {
        session.Generate(Id, file.Path[..^".legacy.json".Length] + ".json", text);
        session.Deletions.Add(file.Path);
        return text;
    }
}
