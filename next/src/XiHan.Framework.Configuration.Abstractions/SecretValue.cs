// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Abstractions;

/// <summary>
/// 一个密钥的值与版本。<see cref="ToString"/> 固定返回遮蔽文本，防止值进入日志或异常消息。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class SecretValue
{
    /// <summary>
    /// 遮蔽文本。
    /// </summary>
    public const string Redacted = "***";

    /// <summary>
    /// 创建。
    /// </summary>
    public SecretValue(string value, string? version = null)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Version = version;
    }

    /// <summary>
    /// 值。
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// 版本；密钥库不提供时为 <see langword="null"/>。
    /// </summary>
    public string? Version { get; }

    /// <inheritdoc />
    public override string ToString() => Redacted;
}
