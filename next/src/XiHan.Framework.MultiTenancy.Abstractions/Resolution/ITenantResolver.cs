// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.MultiTenancy.Abstractions.Resolution;

/// <summary>
/// 从调用上下文解析租户 ID 候选。按注册顺序尝试，先解析出的生效；只解析，不校验。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public interface ITenantResolver
{
    /// <summary>
    /// 解析；无法解析时无值。
    /// </summary>
    ValueTask<Maybe<string>> ResolveAsync(InvocationContext context, CancellationToken cancellationToken);
}
