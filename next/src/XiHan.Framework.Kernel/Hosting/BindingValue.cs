// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 宿主解析出的一个资源绑定。
/// </summary>
/// <remarks>
/// 来源：配置节 <c>XiHan:Bindings:&lt;name&gt;</c>，或 Service Binding 规范的 <c>SERVICE_BINDING_ROOT</c> 目录。
/// </remarks>
/// <param name="Name">绑定名。</param>
/// <param name="Type">资源类型；来源未声明时为空字符串。</param>
/// <param name="Value">主值（连接字符串、URI 等）；没有时为 <see langword="null"/>。</param>
/// <param name="Properties">其余属性。</param>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record BindingValue(string Name, string Type, string? Value, IReadOnlyDictionary<string, string> Properties)
{
    /// <summary>
    /// 取属性。
    /// </summary>
    public Maybe<string> Property(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Properties.TryGetValue(key, out var value) ? Maybe.Some(value) : default;
    }
}
