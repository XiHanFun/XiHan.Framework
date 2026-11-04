// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 已登记实体的目录：实体、表、列、协议与索引
/// </summary>
/// <remarks>
/// 每个以 <c>AddRepository</c> 登记的实体一条（按登记顺序，同一实体登记多次只一条），表名与列名由当前的持久化 Provider 映射出。代码生成据此认出「沿用已有实体」：库里的表由哪个实体建出、表名与列名的大小写写法、主键与协议字段。结果在首次调用时算出并缓存（映射在装配后不再变化）；不连库
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IEntityCatalog
{
    /// <summary>
    /// 全部已登记实体的映射
    /// </summary>
    /// <exception cref="InvalidOperationException">实体的索引声明不合法，或实体不在 Provider 的模型里（装配错误）</exception>
    ValueTask<IReadOnlyList<EntityMapping>> ListAsync(CancellationToken cancellationToken);
}
