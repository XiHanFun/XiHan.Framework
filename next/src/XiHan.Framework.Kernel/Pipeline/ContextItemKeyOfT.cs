// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// <see cref="InvocationContext.Items"/> 的强类型键。
/// </summary>
/// <remarks>
/// 用静态只读实例作为键，避免字符串键冲突。
/// </remarks>
/// <typeparam name="T">值类型。</typeparam>
/// <param name="name">键名，仅用于诊断。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed class ContextItemKey<T>(string name)
{
    /// <summary>
    /// 键名。
    /// </summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <inheritdoc />
    public override string ToString() => Name;
}
