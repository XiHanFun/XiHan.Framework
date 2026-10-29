// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using SqlSugar;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.SqlSugar;

/// <summary>
/// 决定一种实体在作用域客户端的哪个连接上读写（实体级落点）。
/// </summary>
/// <remarks>
/// 默认实现是 <see cref="SqlSugarConnectionSelector"/>；应用在装配前注册自己的实现即可替换规则。同一工作单元内对同一实体类型的选择结果必须稳定。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IEntityConnectionSelector
{
    /// <summary>
    /// 为实体类型选择连接。
    /// </summary>
    /// <remarks>
    /// 返回 <paramref name="client"/> 本身表示根（默认）连接，否则返回 <c>client.AsTenant().GetConnection(configId)</c> 取得的连接。规则指向的连接不存在是配置错误，抛出 <see cref="FrameworkException"/>。
    /// </remarks>
    /// <param name="entityType">实体类型。</param>
    /// <param name="client">当前作用域的客户端（可能带多个连接）。</param>
    ISqlSugarClient ConnectionFor(Type entityType, ISqlSugarClient client);
}
