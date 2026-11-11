// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 操作契约的运行期形态
/// </summary>
/// <remarks>
/// 操作 ID、稳定性、暴露的协议、授权要求与是否只读。与 <see cref="OperationAttribute"/> 字段一一对应，由登记代码（手写或生成）提供。
/// 构造函数只收 ID，其余字段是 <c>init</c> 属性：以后加字段只加属性，已编译的登记代码不受影响（ADR-0136）；字段之间的约束在赋值时校验，违反即抛出 <see cref="ArgumentException"/>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record OperationContract
{
    /// <summary>
    /// 授权策略名的存储
    /// </summary>
    private readonly string? _policy;

    /// <summary>
    /// 所需权限码的存储
    /// </summary>
    private readonly string? _permission;

    /// <summary>
    /// 声明的只读
    /// </summary>
    private readonly bool _declaredReadOnly;

    /// <summary>
    /// 创建契约
    /// </summary>
    /// <param name="id">符合 <see cref="OperationId"/> 文法的操作 ID</param>
    public OperationContract(string id)
    {
        Id = OperationId.Validate(id);
    }

    /// <summary>
    /// 操作 ID
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 稳定性
    /// </summary>
    /// <remarks>
    /// 缺省 <see cref="Stability.Stable"/>
    /// </remarks>
    public Stability Stability { get; init; } = Stability.Stable;

    /// <summary>
    /// 暴露的协议
    /// </summary>
    /// <remarks>
    /// 缺省 <see cref="Operations.Expose.All"/>，即 5.0 的五个协议
    /// </remarks>
    public Expose Expose { get; init; } = Expose.All;

    /// <summary>
    /// 授权策略名
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Permission"/> 二选一；为 <see langword="null"/> 时看 <see cref="Permission"/>，都为 <see langword="null"/> 则无授权要求
    /// </remarks>
    public string? Policy
    {
        get => _policy;
        init
        {
            // 两个字段各在自己的赋值里查对方：无论初始化器先写哪个，后写的那个都会发现冲突
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The policy name must not be blank.", nameof(Policy));
            }

            if (value is not null && _permission is not null)
            {
                throw new ArgumentException("Specify either a policy or a permission, not both.", nameof(Policy));
            }

            _policy = value;
        }
    }

    /// <summary>
    /// 所需权限码
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Policy"/> 二选一
    /// </remarks>
    public string? Permission
    {
        get => _permission;
        init
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The permission code must not be blank.", nameof(Permission));
            }

            if (value is not null && _policy is not null)
            {
                throw new ArgumentException("Specify either a policy or a permission, not both.", nameof(Permission));
            }

            _permission = value;
        }
    }

    /// <summary>
    /// 是否只读（不改变状态、可重复调用）
    /// </summary>
    /// <remarks>
    /// 动词是 <c>get</c> / <c>list</c>，或声明了只读的非标准动词；<c>create</c> / <c>update</c> / <c>delete</c> 不能声明只读。
    /// 协议据此推导 MCP 行为提示与 HTTP 方法；装配了持久化时 Transaction 阶段据此开只读工作单元（不开数据库事务，写入以 <c>PERSISTENCE_UNEXPECTED_001</c> 拒绝）
    /// </remarks>
    public bool IsReadOnly
    {
        get => _declaredReadOnly || Verb is "get" or "list";
        init
        {
            if (value && Verb is "create" or "update" or "delete")
            {
                throw new ArgumentException($"Operation '{Id}' cannot be declared read-only: the '{Verb}' verb changes state.", nameof(IsReadOnly));
            }

            _declaredReadOnly = value;
        }
    }

    /// <summary>
    /// 资源段
    /// </summary>
    public string Resource => OperationId.ResourceOf(Id);

    /// <summary>
    /// 动词段
    /// </summary>
    public string Verb => OperationId.VerbOf(Id);

    /// <summary>
    /// 是否暴露给指定协议
    /// </summary>
    /// <remarks>
    /// <see cref="Operations.Expose.None"/> 永远为 <see langword="false"/>
    /// </remarks>
    public bool IsExposedTo(Expose protocol) => protocol != Expose.None && (Expose & protocol) == protocol;
}
