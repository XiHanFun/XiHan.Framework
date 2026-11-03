// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Knowledge;

/// <summary>
/// 一个 Options 类读取的配置节
/// </summary>
internal sealed record ConfigSectionEntry
{
    /// <summary>
    /// 节路径，即 Options 类的 <c>ConfigurationSection</c>
    /// </summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>
    /// Options 类的完全限定名
    /// </summary>
    public string Options { get; set; } = string.Empty;

    /// <summary>
    /// 所属包 ID
    /// </summary>
    public string Package { get; set; } = string.Empty;

    /// <summary>
    /// 作为连接信息来源的绑定名
    /// </summary>
    /// <remarks>
    /// <c>FromConfiguration</c> 带绑定参数时才有
    /// </remarks>
    public string? Binding { get; set; }

    /// <summary>
    /// 键里占位符的示例取值
    /// </summary>
    /// <remarks>
    /// 没有列出的占位符示例为 <c>sample</c>
    /// </remarks>
    public IReadOnlyDictionary<string, string>? Placeholders { get; set; }

    /// <summary>
    /// 读取的键，按读取顺序
    /// </summary>
    public IReadOnlyList<ConfigKeyEntry> Keys { get; set; } = [];
}
