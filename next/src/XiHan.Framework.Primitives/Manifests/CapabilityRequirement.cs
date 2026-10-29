// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 清单 <c>requires</c> 中的一项。
/// </summary>
/// <remarks>
/// 依赖的能力、接口版本与是否可选。文本形式 <c>id@version[?]</c>，版本前允许 <c>^</c>（接口版本是整数，<c>^1</c> 与 <c>1</c> 等价）。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record CapabilityRequirement
{
    /// <summary>
    /// 创建依赖。
    /// </summary>
    /// <param name="id">能力 ID。</param>
    /// <param name="version">接口版本，正整数。</param>
    /// <param name="optional">是否可选；可选依赖缺失不阻止启动，特性进入 Degraded。</param>
    public CapabilityRequirement(string id, int version, bool optional = false)
    {
        Id = CapabilityId.Validate(id);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        Version = version;
        Optional = optional;
    }

    /// <summary>
    /// 能力 ID。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 接口版本。
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// 是否可选。
    /// </summary>
    public bool Optional { get; }

    /// <summary>
    /// 声明是否满足本依赖：能力 ID 相同且接口版本相同。
    /// </summary>
    public bool IsSatisfiedBy(CapabilityDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return string.Equals(declaration.Id, Id, StringComparison.Ordinal) && declaration.Version == Version;
    }

    /// <summary>
    /// 解析 <c>id@version[?]</c> 形式，例如 <c>multitenancy.context@^1?</c>。
    /// </summary>
    public static CapabilityRequirement Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out var requirement)
            ? requirement
            : throw new ArgumentException($"'{text}' is not a valid capability requirement; expected '<capability-id>@[^]<version>[?]'.", nameof(text));
    }

    /// <summary>
    /// 尝试解析 <c>id@version[?]</c> 形式。
    /// </summary>
    public static bool TryParse(string? text, out CapabilityRequirement requirement)
    {
        // 失败时 out 参数不应被读取；null! 只是为了满足定赋值要求。
        requirement = null!;
        if (text is null)
        {
            return false;
        }

        // 先剥掉末尾的可选标记，再按 '@' 拆分能力 ID 与版本。
        var optional = text.EndsWith('?');
        var body = optional ? text[..^1] : text;
        var at = body.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || !CapabilityId.IsValid(body[..at]))
        {
            return false;
        }

        // 接口版本是整数，'^' 只是兼容 SemVer 习惯的书写，语义上与不带前缀相同。
        var versionText = body[(at + 1)..];
        if (versionText.StartsWith('^'))
        {
            versionText = versionText[1..];
        }

        if (!int.TryParse(versionText, NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 1)
        {
            return false;
        }

        requirement = new CapabilityRequirement(body[..at], version, optional);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Id}@{Version}{(Optional ? "?" : string.Empty)}");
}
