// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json.Serialization;
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 宿主侧的操作：把调用方主体回显出来，供插件回调测试断言身份。
/// </summary>
internal sealed class PingOperation : IOperation<PingRequest, PingResponse>
{
    /// <inheritdoc />
    public ValueTask<Result<PingResponse>> HandleAsync(PingRequest request, InvocationContext context)
    {
        var principal = context.GetSecurity().Principal;
        return ValueTask.FromResult(Result.Success(new PingResponse(request.Name, principal.Id, principal.AuthenticationScheme)));
    }
}

/// <summary>
/// 请求。
/// </summary>
internal sealed record PingRequest(string Name);

/// <summary>
/// 响应：名字、调用方主体 ID 与认证方案。
/// </summary>
internal sealed record PingResponse(string Name, string Principal, string? Scheme);

/// <summary>
/// 测试的序列化上下文。
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PingRequest))]
[JsonSerializable(typeof(PingResponse))]
internal sealed partial class PingJsonContext : JsonSerializerContext;
