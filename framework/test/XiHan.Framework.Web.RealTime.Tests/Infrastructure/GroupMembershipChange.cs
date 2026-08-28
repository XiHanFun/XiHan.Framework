// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.RealTime.Tests.Infrastructure;

/// <summary>
/// 一次组成员变更的记录
/// </summary>
/// <param name="ConnectionId">连接 ID</param>
/// <param name="GroupName">组名</param>
public sealed record GroupMembershipChange(string ConnectionId, string GroupName);
