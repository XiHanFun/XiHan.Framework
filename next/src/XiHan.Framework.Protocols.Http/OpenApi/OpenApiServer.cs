// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// OpenAPI 文档 <c>servers</c> 里的一项：服务地址与可选说明。
/// </summary>
/// <param name="Url">服务地址（绝对 URL 或相对路径，如 <c>https://api.example.com</c>、<c>/api</c>）。</param>
/// <param name="Description">说明；为空不写。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OpenApiServer(string Url, string? Description = null)
{
    /// <summary>
    /// 服务地址。
    /// </summary>
    public string Url { get; } = !string.IsNullOrWhiteSpace(Url) ? Url : throw new ArgumentException("The server URL must not be empty.", nameof(Url));
}
