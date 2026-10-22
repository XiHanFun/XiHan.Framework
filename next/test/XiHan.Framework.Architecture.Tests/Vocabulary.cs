// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 从 <c>spec/conventions/vocabulary.md</c> 的表格读取术语表。规范文件是唯一事实源，测试不另存一份。
/// </summary>
internal static class Vocabulary
{
    /// <summary>
    /// 结构词：不是子系统领域，但合法出现在包 ID 中的词。
    /// </summary>
    public static readonly IReadOnlySet<string> StructuralWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "Primitives", "Kernel", "Domain", "Hosting", "Protocols", "Plugins", "Profiles", "Testing", "Cli", "Compat",
    };

    /// <summary>
    /// 术语表文件路径。
    /// </summary>
    public static string FilePath => Path.Combine(Workspace.SpecRoot, "conventions", "vocabulary.md");

    /// <summary>
    /// 术语表全部条目，按文件顺序。
    /// </summary>
    public static IReadOnlyList<VocabularyEntry> Entries { get; } = Load();

    /// <summary>
    /// 全部领域词。
    /// </summary>
    public static IReadOnlySet<string> Words { get; } = Entries.Select(static e => e.Word).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 子系统领域词：领域词中去掉结构词。
    /// </summary>
    public static IReadOnlySet<string> SubsystemWords { get; } =
        Words.Where(static w => !StructuralWords.Contains(w)).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// 解析 Markdown 表格：只取以 <c>| `</c> 开头的数据行（表头与分隔行不以反引号开头），前四列依次为领域词、能力 ID 前缀、配置节、错误码域。
    /// </summary>
    private static List<VocabularyEntry> Load()
    {
        var entries = new List<VocabularyEntry>();
        foreach (var line in File.ReadAllLines(FilePath))
        {
            if (!line.StartsWith("| `", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Split('|', StringSplitOptions.TrimEntries);
            // 分割后首尾各有一个空单元
            if (cells.Length < 6)
            {
                continue;
            }

            entries.Add(new VocabularyEntry(
                Strip(cells[1]),
                Strip(cells[2]),
                Strip(cells[3]),
                Strip(cells[4])));
        }

        return entries;
    }

    /// <summary>
    /// 去掉单元格两端的空白与反引号。
    /// </summary>
    private static string Strip(string cell) => cell.Trim().Trim('`').Trim();
}
