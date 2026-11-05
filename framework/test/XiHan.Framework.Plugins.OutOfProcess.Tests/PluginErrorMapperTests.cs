// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Grpc.Core;
using XiHan.Framework.Kernel;
using XiHan.Framework.Plugins.Protocol;
using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// gRPC 状态到宿主错误的映射（<c>spec/plugin/invocation.md</c> §3）
/// </summary>
public sealed class PluginErrorMapperTests
{
    [Theory]
    [InlineData(StatusCode.Unavailable, PluginErrors.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded, KernelErrors.DeadlineExceeded)]
    [InlineData(StatusCode.Cancelled, PluginErrors.Unavailable)]
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FromRpc_KeepsCancelled_WhenTheCallerCancelled(bool processExited)
    {
        // 令牌已触发才是取消：映射不按时钟判断是不是超时，截止时间到了的由 DeadlineMiddleware 按截止令牌改报超时（RFC-0004）
        var error = PluginErrorMapper.FromRpc(new RpcException(new Grpc.Core.Status(StatusCode.Cancelled, "cancelled")), "xihan.samples.echo", "echoes.create", LifecycleState.Ready, processExited, callerCancelled: true);

        Assert.Equal(KernelErrors.Cancelled, error.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FromRpc_ReportsUnavailable_WhenCancelledWithoutTheCaller(bool processExited)
    {
        // 调用方没取消：插件停机或通道释放切断了调用，进程退没退都一样（ADR-0127）
        var error = PluginErrorMapper.FromRpc(new RpcException(new Grpc.Core.Status(StatusCode.Cancelled, "gRPC call disposed.")), "xihan.samples.echo", "echoes.create", LifecycleState.Draining, processExited, callerCancelled: false);

        Assert.Equal(PluginErrors.Unavailable, error.Code);
    }

    [Fact]
    public void FromRpc_KeepsDeadline_EvenWhenTheProcessHasExited()
    {
        var error = PluginErrorMapper.FromRpc(new RpcException(new Grpc.Core.Status(StatusCode.DeadlineExceeded, "deadline")), "xihan.samples.echo", "echoes.create", LifecycleState.Ready, processExited: true);

        Assert.Equal(KernelErrors.DeadlineExceeded, error.Code);
    }
}
