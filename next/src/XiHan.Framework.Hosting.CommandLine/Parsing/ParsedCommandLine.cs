// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.CommandLine.Parsing;

/// <summary>
/// 解析后的命令行：命令名、位置参数与选项（同名选项可重复，值按出现顺序）。
/// </summary>
/// <param name="Command">第一个非选项记号；没有时为 <see langword="null"/>。</param>
/// <param name="Arguments">命令名之后的位置参数。</param>
/// <param name="Options">选项名（不含 <c>--</c>）→ 值；无值的标志记为 <c>true</c>。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ParsedCommandLine(string? Command, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, IReadOnlyList<string>> Options)
{
    /// <summary>
    /// 选项的最后一个值。
    /// </summary>
    public Maybe<string> Option(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Options.TryGetValue(name, out var values) && values.Count > 0 ? values[^1] : default(Maybe<string>);
    }

    /// <summary>
    /// 选项的全部值。
    /// </summary>
    /// <remarks>
    /// 没有时为空。
    /// </remarks>
    public IReadOnlyList<string> OptionValues(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Options.TryGetValue(name, out var values) ? values : [];
    }

    /// <summary>
    /// 是否给出了该选项（含无值的标志）。
    /// </summary>
    public bool HasOption(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Options.ContainsKey(name);
    }
}
