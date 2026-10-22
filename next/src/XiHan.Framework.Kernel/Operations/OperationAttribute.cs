// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 把一个 <see cref="IOperation{TRequest, TResponse}"/> 实现声明为操作契约：操作 ID、稳定性、暴露的协议与授权要求。
/// 内核零反射，运行期不读本特性；它是源生成器（<c>Kernel.Generators</c>，生成注册代码与 Schema）与工具链的输入，
/// 运行期以 <see cref="OperationContract"/> 经 <c>FeatureContext.AddOperation</c> 登记。
/// </summary>
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
    /// 稳定性；默认 <see cref="Stability.Stable"/>。
    /// </summary>
    public Stability Stability { get; init; } = Stability.Stable;

    /// <summary>
    /// 暴露的协议；默认 <see cref="Expose.All"/>。
    /// </summary>
    public Expose Expose { get; init; } = Expose.All;

    /// <summary>
    /// 授权策略名；与 <see cref="Permission"/> 二选一。
    /// </summary>
    public string? Policy { get; init; }

    /// <summary>
    /// 所需权限码；与 <see cref="Policy"/> 二选一。
    /// </summary>
    public string? Permission { get; init; }
}
