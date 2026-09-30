// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ProviderContractTests;

/// <summary>
/// 提供方声明的能力，契约用例按能力决定执行或跳过
/// </summary>
[Flags]
public enum ProviderCapabilities
{
    /// <summary>
    /// 不声明任何附加能力
    /// </summary>
    None = 0,

    /// <summary>
    /// 持久化：一个客户端写入的数据可被夹具新建的其他客户端读到
    /// </summary>
    Persistence = 1 << 0,

    /// <summary>
    /// 跨实例独占领取：一个客户端领取的记录在领取有效期内不会被其他客户端领到
    /// </summary>
    ExclusiveClaim = 1 << 1,

    /// <summary>
    /// 领取过期：夹具可让当前全部领取立即过期，过期后记录可被重新领取
    /// </summary>
    ClaimExpiry = 1 << 2,

    /// <summary>
    /// 可控时间：夹具可推进提供方判断到期所用的时间
    /// </summary>
    ControllableTime = 1 << 3,

    /// <summary>
    /// 收件箱按消息标识去重入箱
    /// </summary>
    Deduplication = 1 << 4,

    /// <summary>
    /// 收件箱已完结（已处理或已丢弃）的记录不会被延后重试回退为待处理
    /// </summary>
    FinalStateProtection = 1 << 5,

    /// <summary>
    /// 并发存储：底层是支持真实并发写入的数据库，可执行多工作者并发领取
    /// </summary>
    ConcurrentStorage = 1 << 6
}
