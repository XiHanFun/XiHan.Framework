// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 宿主解析出的一个资源绑定
/// </summary>
/// <remarks>
/// 来源：配置节 <c>XiHan:Bindings:&lt;name&gt;</c>，或 Service Binding 规范的 <c>SERVICE_BINDING_ROOT</c> 目录。四项都是必填的初始化成员，没有位置构造与解构：以后加字段只加可选的初始化成员，不破坏已有的构造处（ADR-0142、ADR-0267）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record BindingValue
{
    /// <summary>
    /// 绑定名
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 资源类型
    /// </summary>
    /// <remarks>
    /// 来源未声明时为空字符串
    /// </remarks>
    public required string Type { get; init; }

    /// <summary>
    /// 主值（连接字符串、URI 等）
    /// </summary>
    /// <remarks>
    /// 没有时为 <see langword="null"/>
    /// </remarks>
    public required string? Value { get; init; }

    /// <summary>
    /// 其余属性
    /// </summary>
    /// <remarks>
    /// 没有其余属性时是空字典
    /// </remarks>
    public required IReadOnlyDictionary<string, string> Properties { get; init; }

    /// <summary>
    /// 取属性
    /// </summary>
    public Maybe<string> Property(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Properties.TryGetValue(key, out var value) ? Maybe.Some(value) : default;
    }

    /// <summary>
    /// 打印成员，凭据以 <see cref="Redaction.Mask"/> 代替
    /// </summary>
    /// <remarks>
    /// 记录的 <c>ToString()</c> 经它输出：对象可能整个进日志与异常消息，主值（连接串或带用户信息的 URI）不能明文出现（RFC-0014「选项里的凭据」）。其余成员同编译器生成的写法；新加属性时在这里一并补上，架构测试 <c>CredentialRedactionTests</c> 会核对
    /// </remarks>
    private bool PrintMembers(StringBuilder builder)
    {
        // 凭据只打印遮蔽文本，null 仍打印为空：看得出有没有配置，看不到值
        builder.Append("Name = ").Append(Name)
            .Append(", Type = ").Append(Type)
            .Append(", Value = ").Append(Redaction.Redact(Value))
            .Append(", Properties = ").Append(Properties);
        return true;
    }
}
