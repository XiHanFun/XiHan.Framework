// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Localization.Abstractions.Messages;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Grpc;

/// <summary>
/// gRPC 协议适配器的消息键；各语言文案在本包的 <c>Localization/&lt;区域&gt;.json</c>，特性装配时注册 <see cref="Catalog"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class GrpcMessages
{
    /// <summary>
    /// 目录名。
    /// </summary>
    public const string CatalogName = "protocols.grpc";

    /// <summary>
    /// 请求不是合法的 protobuf 消息。参数：<c>operation</c>、<c>reason</c>。
    /// </summary>
    public const string RequestMalformed = "protocols.grpc.request_malformed";

    /// <summary>
    /// 响应无法编码为 protobuf。参数：<c>operation</c>。
    /// </summary>
    public const string ResponseUnencodable = "protocols.grpc.response_unencodable";

    /// <summary>
    /// 本包的消息目录。
    /// </summary>
    public static MessageCatalog Catalog => MessageCatalog.FromEmbeddedResources(CatalogName, typeof(GrpcMessages));
}
