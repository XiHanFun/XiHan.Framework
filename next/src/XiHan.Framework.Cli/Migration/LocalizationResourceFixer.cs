// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-09：<c>Localization/</c> 下 4.x 的 <c>{ resource, culture, texts }</c> 资源文件转成每个区域一个 <c>Localization/&lt;区域&gt;.json</c>（平铺键 <c>&lt;领域&gt;.&lt;资源&gt;.&lt;键&gt;</c>，多个资源合并，占位符原样），
/// 原文件删除；项目文件补嵌入资源项并留标记提醒装配目录。
/// </summary>
internal sealed class LocalizationResourceFixer : V4Fixer
{
    /// <inheritdoc />
    public override string Id => "V4-09";

    /// <inheritdoc />
    public override bool Handles(MigrationFile file)
        => file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Path.GetFileName(Path.GetDirectoryName(file.Path)), "Localization", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override void Scan(MigrationFile file, string text, MigrationSession session)
    {
        if (file.ProjectDirectory is null || !V4LocalizationGenerator.TryParse(text, out var resource, out var culture, out var texts))
        {
            return;
        }

        var domain = V4LocalizationGenerator.Domain(V4ServicesGenerator.Names(file.ProjectDirectory).Project);
        if (!session.LocalizationResources.TryGetValue(file.ProjectDirectory, out var cultures))
        {
            cultures = new Dictionary<string, SortedDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            session.LocalizationResources[file.ProjectDirectory] = cultures;
        }

        if (!cultures.TryGetValue(culture, out var entries))
        {
            entries = new SortedDictionary<string, string>(StringComparer.Ordinal);
            cultures[culture] = entries;
        }

        foreach (var (key, value) in texts)
        {
            entries[V4LocalizationGenerator.Key(domain, resource, key)] = value;
        }

        // 原文件转完就删，否则会被 Localization/*.json 的嵌入项一起打包
        session.Deletions.Add(file.Path);
        session.Changed(Id, file, 0);
    }

    /// <inheritdoc />
    public override void Prepare(MigrationSession session)
    {
        foreach (var (project, cultures) in session.LocalizationResources.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var newLine = session.ProjectNewLine(project);
            foreach (var (culture, entries) in cultures.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                var path = Path.Combine(project, "Localization", culture + ".json");
                // 已有的区域文件保留其余键，本次转出的键覆盖同名项
                var merged = File.Exists(path) ? V4LocalizationGenerator.ReadExisting(File.ReadAllText(path)) : new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (key, value) in entries)
                {
                    merged[key] = value;
                }

                var content = V4LocalizationGenerator.Write(merged, newLine);
                if (File.Exists(path) && string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal))
                {
                    continue;
                }

                session.GeneratedFiles[path] = content;
                session.Changed(Id, new MigrationFile(path, session.RelativePath(path), project, newLine), 0);
            }
        }
    }

    /// <inheritdoc />
    public override string Apply(MigrationFile file, string text, MigrationSession session) => text;
}
