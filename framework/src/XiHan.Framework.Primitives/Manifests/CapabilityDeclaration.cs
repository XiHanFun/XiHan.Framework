// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// 清单 <c>provides</c> 中的一项：提供的能力及其接口版本
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record CapabilityDeclaration
{
    /// <summary>
    /// 创建声明
    /// </summary>
    /// <param name="id">能力 ID</param>
    /// <param name="version">接口版本，正整数</param>
    public CapabilityDeclaration(string id, int version)
    {
        Id = CapabilityId.Validate(id);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        Version = version;
    }

    /// <summary>
    /// 能力 ID
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 接口版本
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// 解析 <c>id@version</c> 形式，例如 <c>persistence.relational@1</c>
    /// </summary>
    public static CapabilityDeclaration Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return TryParse(text, out var declaration)
            ? declaration
            : throw new ArgumentException($"'{text}' is not a valid capability declaration; expected '<capability-id>@<version>'.", nameof(text));
    }

    /// <summary>
    /// 尝试解析 <c>id@version</c> 形式
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out CapabilityDeclaration? declaration)
    {
        declaration = null;
        if (text is null)
        {
            return false;
        }

        var at = text.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0 || !CapabilityId.IsValid(text[..at]))
        {
            return false;
        }

        // 版本是不带前导零、符号与空白的正整数，在 32 位范围内（ADR-0236）
        if (!InterfaceVersionText.TryParse(text.AsSpan(at + 1), out var version))
        {
            return false;
        }

        declaration = new CapabilityDeclaration(text[..at], version);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Id}@{Version}");
}
