// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Migration;

namespace XiHan.Framework.Cli.Tests.Migration;

/// <summary>
/// 测试规则 <c>T5-02</c>。
/// </summary>
/// <remarks>
/// 第一遍记下原文里用到 <c>LegacyClock</c> 的文件，扫描完在根目录生成清单 <c>MIGRATION.md</c>；不改任何文件。排在 <see cref="RenameClockFixer"/> 之后，能看到原文说明扫描先于改写。
/// </remarks>
internal sealed class InventoryFixer : MigrationFixer
{
    /// <inheritdoc />
    public override string Id => "T5-02";

    /// <inheritdoc />
    public override bool Handles(MigrationFile file) => file.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override void Scan(MigrationFile file, string text, MigrationSession session)
    {
        if (text.Contains("LegacyClock", StringComparison.Ordinal))
        {
            session.StateOf<InventoryState>().Files.Add(file.RelativePath);
        }
    }

    /// <inheritdoc />
    public override void Prepare(MigrationSession session)
    {
        var files = session.StateOf<InventoryState>().Files;
        if (files.Count > 0)
        {
            session.Generate(Id, Path.Combine(session.Root, "MIGRATION.md"), string.Join('\n', files.Order(StringComparer.Ordinal)) + "\n");
        }
    }

    /// <inheritdoc />
    public override string Apply(MigrationFile file, string text, MigrationSession session) => text;
}
