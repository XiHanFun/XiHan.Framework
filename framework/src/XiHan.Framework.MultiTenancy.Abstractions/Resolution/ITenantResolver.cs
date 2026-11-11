// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Resolution;

/// <summary>
/// 从调用上下文解析租户 ID 候选
/// </summary>
/// <remarks>
/// 按注册顺序尝试，先解析出的生效；解析器只给出候选，格式、存在与启用由 Tenant 阶段核对
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public interface ITenantResolver
{
    /// <summary>
    /// 解析
    /// </summary>
    /// <remarks>
    /// 三种结果：
    /// <list type="bullet">
    /// <item>成功且有值：候选租户 ID，后面的解析器不再调用</item>
    /// <item>成功且无值：这个解析器给不出租户，交给下一个解析器</item>
    /// <item>失败：拒绝这次调用（例如候选互相矛盾、调用方不属于该租户、映射所需的存储不可达），Tenant 阶段以这个错误结束调用，不再问后面的解析器（ADR-0143）</item>
    /// </list>
    /// </remarks>
    ValueTask<Result<Maybe<string>>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken);
}
