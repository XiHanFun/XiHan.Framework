// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;

namespace XiHan.Framework.Upgrade.Tests.Fakes;

/// <summary>
/// 应用信息访问器的手写替身
/// </summary>
/// <remarks>
/// 升级引擎在未显式配置节点名时用实例标识拼节点名，替身把实例标识固定下来，
/// 让节点名解析可断言。
/// </remarks>
public sealed class FakeApplicationInfoAccessor : IApplicationInfoAccessor
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="instanceId">实例标识</param>
    /// <param name="applicationName">应用名称</param>
    public FakeApplicationInfoAccessor(string instanceId = "test-instance", string? applicationName = "XiHan.Framework.Upgrade.Tests")
    {
        InstanceId = instanceId;
        ApplicationName = applicationName;
    }

    /// <summary>
    /// 应用名称
    /// </summary>
    public string? ApplicationName { get; }

    /// <summary>
    /// 实例标识
    /// </summary>
    public string InstanceId { get; }
}
