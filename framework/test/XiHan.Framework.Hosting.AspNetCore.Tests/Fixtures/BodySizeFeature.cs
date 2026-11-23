// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.AspNetCore.Http.Features;

namespace XiHan.Framework.Hosting.AspNetCore.Tests.Fixtures;

/// <summary>
/// 可改写的请求体上限特性
/// </summary>
/// <remarks>
/// 代替服务器对这次请求的请求体上限
/// </remarks>
internal sealed class BodySizeFeature : IHttpMaxRequestBodySizeFeature
{
    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <inheritdoc />
    public long? MaxRequestBodySize { get; set; }
}
