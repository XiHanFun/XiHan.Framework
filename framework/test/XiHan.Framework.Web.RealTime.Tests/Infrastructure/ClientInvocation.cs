// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.RealTime.Tests.Infrastructure;

/// <summary>
/// 一次客户端发送调用的记录
/// </summary>
/// <param name="Method">客户端方法名</param>
/// <param name="Args">调用参数</param>
public sealed record ClientInvocation(string Method, object?[] Args);
