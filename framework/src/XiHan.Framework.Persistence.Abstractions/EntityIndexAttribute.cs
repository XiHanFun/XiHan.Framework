// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions;

/// <summary>
/// 在实体类上声明一个数据库索引
/// </summary>
/// <remarks>
/// 与 <see cref="LongTextAttribute"/> 同一套路：实体只引用本包，Provider 据此建索引——SqlSugar 的约定在 <c>InitTables</c> 时建出，EF Core 的 <c>ApplyPersistenceConventions(Database)</c> 映射为 <c>HasIndex</c>。列按声明顺序、全部升序；同一实体可以声明多个。名字不由调用方给，按 <see cref="EntityIndexes"/> 的规则从表名与列推出（<c>IX_</c> / <c>UX_</c> 前缀、表名、各列缩写），两个 Provider 建出同名的索引。实体实现 <c>ISoftDeletable</c> 时唯一索引只约束未删除的行（部分唯一索引，见 <see cref="EntityIndex.ExcludesDeleted"/>）。不叫 <c>IndexAttribute</c>：EF Core 有同名特性，两个命名空间同时引入时会冲突
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class EntityIndexAttribute : Attribute
{
    /// <summary>
    /// 以属性名声明索引的列
    /// </summary>
    /// <remarks>
    /// 用 <c>nameof</c> 写属性名，按顺序成为索引的列
    /// </remarks>
    public EntityIndexAttribute(params string[] properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        Properties = [.. properties];
    }

    /// <summary>
    /// 索引的列（属性名），按声明顺序
    /// </summary>
    public IReadOnlyList<string> Properties { get; }

    /// <summary>
    /// 是否唯一索引
    /// </summary>
    /// <remarks>
    /// 业务上唯一的键（如租户 + 名字）声明为唯一。关系型 Provider 写入撞上唯一索引时返回 <see cref="PersistenceErrors.UniqueKeyConflict"/>（同一批里两项同键、库里另一行同键），工作单元随后只能回滚。软删除实体只在未删除的行之间唯一；数据库不支持部分索引时启动失败，要么不声明唯一，要么把 <c>IsDeleted</c> 显式写进列（普通唯一索引，同一业务键最多一行已删除）
    /// </remarks>
    public bool IsUnique { get; set; }

    /// <summary>
    /// 多租户实体的唯一索引是否跨租户唯一
    /// </summary>
    /// <remarks>
    /// 实体实现 <c>IMultiTenant</c> 时唯一索引的列必须含 <c>TenantId</c>，撞键只发生在同一租户里（ADR-0198）。确实要跨租户唯一的键（全局唯一的登录名之类）显式设为 <see langword="true"/>，接受撞键告诉调用方别的租户已有这个键。只用在不含 <c>TenantId</c> 的多租户唯一索引上，别处设置是声明错误
    /// </remarks>
    public bool AcrossTenants { get; set; }
}
