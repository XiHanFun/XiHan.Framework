// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Providers;

/// <summary>
/// 按 Provider 配置创建 <see cref="IChatClient"/> 的工厂（<c>ai.chat@1</c> 的 Provider 契约）；以 <c>AddAIChatClientFactory&lt;T&gt;()</c> 登记。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IChatClientFactory
{
    /// <summary>
    /// 匹配 <see cref="AIProviderOptions.Kind"/> 的种类（小写）。
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// 创建客户端；配置缺必填项 → <see cref="AIErrors.RequestInvalid"/>。
    /// </summary>
    Result<IChatClient> Create(AIProviderOptions options);
}
