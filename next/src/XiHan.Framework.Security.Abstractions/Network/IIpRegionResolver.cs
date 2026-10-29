// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Network;

/// <summary>
/// IP 归属地解析（能力 <c>security.ip-region@1</c>）。
/// </summary>
/// <remarks>
/// 条件属性 <c>environment.geo.*</c>、登录日志与命中记录的归属地列用它。宿主可以不装：容器里没有本服务时，调用方把归属地当作取不到。实现须线程安全、不访问网络，查不到时返回无值而不是抛出。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IIpRegionResolver
{
    /// <summary>
    /// 解析归属地。
    /// </summary>
    /// <remarks>
    /// IPv4 映射的 IPv6 地址按 IPv4 查。回环与内网地址、数据库不可用、查不到时为无值。
    /// </remarks>
    Maybe<IpRegion> Resolve(IPAddress address);
}
