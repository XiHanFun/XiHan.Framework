// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 操作契约的运行期形态：操作 ID、稳定性、暴露的协议、授权要求与是否只读。与 <see cref="OperationAttribute"/> 字段一一对应，由登记代码（手写或生成）提供。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record OperationContract
{
    /// <summary>
    /// 创建契约。
    /// </summary>
    /// <param name="id">符合 <see cref="OperationId"/> 文法的操作 ID。</param>
    /// <param name="stability">稳定性。</param>
    /// <param name="expose">暴露的协议。</param>
    /// <param name="policy">授权策略名；与 <paramref name="permission"/> 二选一。</param>
    /// <param name="permission">所需权限码；与 <paramref name="policy"/> 二选一。</param>
    /// <param name="readOnly">声明非标准动词是只读的；<c>get</c> / <c>list</c> 不需要声明，<c>create</c> / <c>update</c> / <c>delete</c> 不能声明。</param>
    public OperationContract(string id, Stability stability = Stability.Stable, Expose expose = Expose.All, string? policy = null, string? permission = null, bool readOnly = false)
    {
        Id = OperationId.Validate(id);
        var verb = OperationId.VerbOf(Id);
        if (readOnly && verb is "create" or "update" or "delete")
        {
            throw new ArgumentException($"Operation '{Id}' cannot be declared read-only: the '{verb}' verb changes state.", nameof(readOnly));
        }
        if (policy is not null && string.IsNullOrWhiteSpace(policy))
        {
            throw new ArgumentException("The policy name must not be blank.", nameof(policy));
        }

        if (permission is not null && string.IsNullOrWhiteSpace(permission))
        {
            throw new ArgumentException("The permission code must not be blank.", nameof(permission));
        }

        if (policy is not null && permission is not null)
        {
            throw new ArgumentException("Specify either a policy or a permission, not both.", nameof(permission));
        }

        Stability = stability;
        Expose = expose;
        Policy = policy;
        Permission = permission;
        IsReadOnly = readOnly || verb is "get" or "list";
    }

    /// <summary>
    /// 是否只读（不改变状态、可重复调用）：动词是 <c>get</c> / <c>list</c>，或声明了只读的非标准动词。协议据此推导 MCP 行为提示与 HTTP 方法。
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    /// 操作 ID。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 稳定性。
    /// </summary>
    public Stability Stability { get; }

    /// <summary>
    /// 暴露的协议。
    /// </summary>
    public Expose Expose { get; }

    /// <summary>
    /// 授权策略名；为 <see langword="null"/> 时看 <see cref="Permission"/>，都为 <see langword="null"/> 则无授权要求。
    /// </summary>
    public string? Policy { get; }

    /// <summary>
    /// 所需权限码。
    /// </summary>
    public string? Permission { get; }

    /// <summary>
    /// 资源段。
    /// </summary>
    public string Resource => OperationId.ResourceOf(Id);

    /// <summary>
    /// 动词段。
    /// </summary>
    public string Verb => OperationId.VerbOf(Id);

    /// <summary>
    /// 是否暴露给指定协议；<see cref="Operations.Expose.None"/> 永远为 <see langword="false"/>。
    /// </summary>
    public bool IsExposedTo(Expose protocol) => protocol != Expose.None && (Expose & protocol) == protocol;
}
