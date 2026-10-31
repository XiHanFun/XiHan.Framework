// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Grpc.Core;
using XiHan.Framework.Kernel;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// gRPC 状态到宿主错误的映射（<c>spec/plugin/invocation.md</c> §3）。
/// </summary>
public sealed class PluginErrorMapperTests
{
    [Theory]
    [InlineData(StatusCode.Unavailable, PluginErrors.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded, KernelErrors.DeadlineExceeded)]
    [InlineData(StatusCode.Cancelled, KernelErrors.Cancelled)]
    [InlineData(StatusCode.Unimplemented, KernelErrors.UnknownOperation)]
    [InlineData(StatusCode.InvalidArgument, KernelErrors.InvalidOperationPayload)]
    [InlineData(StatusCode.Internal, PluginErrors.Unexpected)]
    [InlineData(StatusCode.Unknown, PluginErrors.Unexpected)]
    [InlineData(StatusCode.ResourceExhausted, PluginErrors.Unexpected)]
    [InlineData(StatusCode.FailedPrecondition, PluginErrors.Unexpected)]
    [InlineData(StatusCode.PermissionDenied, PluginErrors.Unexpected)]
    [InlineData(StatusCode.DataLoss, PluginErrors.Unexpected)]
    public void FromRpc_MapsStatusCodes_AsTheSpecTableSays(StatusCode status, string expected)
    {
        var error = PluginErrorMapper.FromRpc(new RpcException(new Grpc.Core.Status(status, "detail")), "xihan.samples.echo", "echoes.create", LifecycleState.Ready);

        Assert.Equal(expected, error.Code);
    }

    [Fact]
    public void FromRpc_KeepsTheStatusAndDetail_ForUnexpectedFailures()
    {
        var error = PluginErrorMapper.FromRpc(new RpcException(new Grpc.Core.Status(StatusCode.Internal, "boom")), "xihan.samples.echo", "echoes.create", LifecycleState.Ready);

        Assert.Equal(PluginErrors.Unexpected, error.Code);
        Assert.Equal("Internal: boom", error.Details!["reason"]);
    }

    [Theory]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.Unknown)]
    [InlineData(StatusCode.Unimplemented)]
    public void FromRpc_ReportsUnavailable_WhenTheProcessHasExited(StatusCode status)
    {
        var error = PluginErrorMapper.FromRpc(new RpcException(new Grpc.Core.Status(status, "connection reset")), "xihan.samples.echo", "echoes.create", LifecycleState.Ready, processExited: true);

        Assert.Equal(PluginErrors.Unavailable, error.Code);
    }

    [Fact]
    public void FromRpc_ReportsATimeout_ForACancellationAfterTheDeadline()
    {
        var error = PluginErrorMapper.FromRpc(new RpcException(new Grpc.Core.Status(StatusCode.Cancelled, "cancelled")), "xihan.samples.echo", "echoes.create", LifecycleState.Ready, deadlineExpired: true, processExited: true);

        Assert.Equal(KernelErrors.DeadlineExceeded, error.Code);
    }
}
