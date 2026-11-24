// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Messaging.Abstractions.Outbox;

/// <summary>
/// 一次探测数出的发件箱状态
/// </summary>
/// <remarks>
/// 各库相加：未投递的行数与死信行数是各库之和，最老一条的等待时间取各库里最长的
/// </remarks>
/// <param name="Pending">未投递的行数：既没有投递成功、也没有进死信，含退避中的</param>
/// <param name="OldestPendingAge">最老的一条未投递的行从创建到探测时刻等了多久；没有未投递的行为 <see langword="null"/></param>
/// <param name="DeadLettered">死信行数</param>
/// <param name="Databases">这次探测数过的库的个数（默认库加分片库）</param>
internal sealed record OutboxBacklog(long Pending, TimeSpan? OldestPendingAge, long DeadLettered, int Databases);
