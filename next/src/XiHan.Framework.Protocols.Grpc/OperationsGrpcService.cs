// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Protocols.Grpc;

/// <summary>
/// gRPC 服务端需要一个服务类型来挂方法；操作的方法由 <see cref="OperationMethodProvider"/> 动态绑定到这个空类型上，它本身没有成员。
/// </summary>
internal sealed class OperationsGrpcService
{
}
