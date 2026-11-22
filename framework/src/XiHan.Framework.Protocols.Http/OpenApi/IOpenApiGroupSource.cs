// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Primitives;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// 运行期增减的 OpenAPI 分组
/// </summary>
/// <remarks>
/// 给运行期才有的归属（插件的清单 ID）分组（ADR-0251）：应用在容器里登记实现，生成文档时它的分组接在 <see cref="OpenApiOptions.Groups"/> 之后，多个来源按登记顺序
/// 合在一起按同样的规则核对：组名唯一且不是 <c>all</c>，一个归属只属于一个组；不合规则是应用的编程错误，生成时抛出 <see cref="ArgumentException"/>
/// 分组变了要触发 <see cref="GetChangeToken"/> 给出的令牌，<c>MapXiHanOpenApi</c> 的文档缓存随之换新；分组文档的地址按请求时的分组答复
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IOpenApiGroupSource
{
    /// <summary>
    /// 当前的分组
    /// </summary>
    IReadOnlyList<OpenApiGroup> Groups { get; }

    /// <summary>
    /// 分组变化的令牌
    /// </summary>
    /// <remarks>
    /// 分组每变一次触发一次，触发之后再取得到新的令牌
    /// </remarks>
    IChangeToken GetChangeToken();
}
