// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;
using XiHan.Framework.Protocols.Grpc.Proto;

namespace XiHan.Framework.Protocols.Grpc.Methods;

/// <summary>
/// 发布给 gRPC 的一个操作：描述、方法名与请求 / 响应的消息描述。
/// </summary>
/// <param name="Descriptor">操作描述。</param>
/// <param name="Method">服务名与方法名。</param>
/// <param name="Request">请求消息。</param>
/// <param name="Response">响应消息。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record GrpcOperation(OperationDescriptor Descriptor, GrpcMethodName Method, ProtoMessage Request, ProtoMessage Response);
