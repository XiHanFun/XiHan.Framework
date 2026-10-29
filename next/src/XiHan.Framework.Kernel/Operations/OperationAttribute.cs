// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 把一个 <see cref="IOperation{TRequest, TResponse}"/> 实现声明为操作契约。
/// </summary>
/// <remarks>
/// 操作 ID、稳定性、暴露的协议与授权要求。内核零反射，运行期不读本特性；它是源生成器（<c>Kernel.Generators</c>，生成注册代码与 Schema）与工具链的输入，运行期以 <see cref="OperationContract"/> 经 <c>FeatureContext.AddOperation</c> 登记。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class OperationAttribute : Attribute
{
    /// <summary>
    /// 声明操作契约。
    /// </summary>
    /// <param name="id">符合 <see cref="OperationId"/> 文法的操作 ID。</param>
    public OperationAttribute(string id)
    {
        Id = OperationId.Validate(id);
    }

    /// <summary>
    /// 操作 ID。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 稳定性。
    /// </summary>
    /// <remarks>
    /// 默认 <see cref="Stability.Stable"/>。
    /// </remarks>
    public Stability Stability { get; init; } = Stability.Stable;

    /// <summary>
    /// 暴露的协议。
    /// </summary>
    /// <remarks>
    /// 没写时取程序集的 <see cref="OperationDefaultsAttribute.Expose"/>，程序集也没声明时为 <see cref="Expose.All"/>。
    /// </remarks>
    public Expose Expose { get; init; } = Expose.All;

    /// <summary>
    /// 声明非标准动词的操作是只读的（不改变状态、可重复调用）。
    /// </summary>
    /// <remarks>
    /// <c>get</c> / <c>list</c> 本来就是只读，<c>tree</c>、<c>list-online</c> 这类查询变体在这里声明。协议据此推导：MCP 的 <c>readOnlyHint</c> / <c>idempotentHint</c> 为真、<c>destructiveHint</c> 为假，HTTP 走 <c>GET …:{verb}</c>（请求带嵌套对象时仍是 POST）。不能用在 <c>create</c> / <c>update</c> / <c>delete</c> 上（生成器报 <c>XHG005</c>，契约构造抛出）。
    /// </remarks>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// 授权策略名。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Permission"/> 二选一。
    /// </remarks>
    public string? Policy { get; init; }

    /// <summary>
    /// 所需权限码。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Policy"/> 二选一。
    /// </remarks>
    public string? Permission { get; init; }
}
