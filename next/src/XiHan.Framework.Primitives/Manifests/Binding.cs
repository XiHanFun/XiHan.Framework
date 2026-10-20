// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 清单 <c>bindings</c> 中的一项：需要宿主注入的资源。缺失必需绑定在启动期失败。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial record Binding
{
    /// <summary>
    /// 创建绑定声明。
    /// </summary>
    /// <param name="name">绑定名，小写字母、数字、连字符。</param>
    /// <param name="type">资源类型，例如 <c>connection:relational</c>、<c>cache:distributed</c>、<c>queue</c>、<c>blob</c>。</param>
    /// <param name="required">是否必需。</param>
    public Binding(string name, string type, bool required = true)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(type);
        if (!NamePattern().IsMatch(name))
        {
            throw new ArgumentException($"绑定名 '{name}' 必须是小写字母开头的小写字母、数字、连字符。", nameof(name));
        }

        if (!TypePattern().IsMatch(type))
        {
            throw new ArgumentException($"绑定类型 '{type}' 必须是 <类别>[:<子类>] 形式的小写标识。", nameof(type));
        }

        Name = name;
        Type = type;
        Required = required;
    }

    /// <summary>
    /// 绑定名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 资源类型。
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// 是否必需。
    /// </summary>
    public bool Required { get; }

    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex NamePattern();

    [GeneratedRegex("^[a-z]+(:[a-z]+)?$")]
    private static partial Regex TypePattern();
}
