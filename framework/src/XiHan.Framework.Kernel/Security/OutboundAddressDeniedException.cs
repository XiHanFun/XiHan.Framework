// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Security;

/// <summary>
/// 出站地址策略拒绝了这次连接
/// </summary>
/// <remarks>
/// 经 <see cref="HttpClient"/> 发请求时，它是抛出的 <see cref="HttpRequestException"/> 的 <see cref="Exception.InnerException"/>。消息只写请求里的主机名，不写解析出的地址：异常消息可能经调用方到达租户，解析结果是内网信息，只进日志
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class OutboundAddressDeniedException : Exception
{
    /// <summary>
    /// 创建
    /// </summary>
    /// <param name="host">请求里的主机名</param>
    /// <param name="port">端口</param>
    public OutboundAddressDeniedException(string host, int port)
        : base($"The outbound address policy does not allow connecting to '{host}'.")
    {
        Host = host;
        Port = port;
    }

    /// <summary>
    /// 请求里的主机名
    /// </summary>
    public string Host { get; }

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; }
}
