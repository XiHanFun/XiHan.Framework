// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// OpenAPI 文档 <c>servers</c> 里的一项：服务地址与可选说明
/// </summary>
/// <remarks>
/// 地址经构造给出，说明经初始化器给出，以后加字段只加属性、不改构造签名（ADR-0142）
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OpenApiServer
{
    /// <summary>
    /// 创建一项服务地址
    /// </summary>
    /// <param name="url">服务地址（绝对 URL 或相对路径，如 <c>https://api.example.com</c>、<c>/api</c>）</param>
    /// <exception cref="ArgumentException"><paramref name="url"/> 为空或空白</exception>
    public OpenApiServer(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("The server URL must not be empty.", nameof(url));
        }

        Url = url;
    }

    /// <summary>
    /// 服务地址
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// 说明
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 不写
    /// </remarks>
    public string? Description { get; init; }
}
