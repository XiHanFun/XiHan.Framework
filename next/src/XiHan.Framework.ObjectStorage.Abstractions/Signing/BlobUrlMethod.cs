// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Signing;

/// <summary>
/// 预签名地址允许的 HTTP 方法。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public enum BlobUrlMethod
{
    /// <summary>
    /// 下载（<c>GET</c>，支持 <c>Range</c> 请求）。
    /// </summary>
    Get = 0,

    /// <summary>
    /// 上传整个对象或一个分片（<c>PUT</c>，请求体即内容）。
    /// </summary>
    Put = 1,
}
