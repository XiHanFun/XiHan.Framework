// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Core.Tests.Fakes;

namespace XiHan.Framework.Core.Tests.Modularity;

/// <summary>
/// 曦寒模块发现的日志管道测试
/// </summary>
/// <remarks>
/// 模块发现接收一个可选的 <see cref="ILogger"/>，调用方（ModuleLoader、PlugInSourceExtensions）
/// 传进来的是初始化日志器，其条目会在容器就绪后被回放到真正的日志管道。
/// 因此模块树必须真的写进这个 logger，而不是只落到静态日志助手的控制台输出，
/// 否则启动期日志回放会整段丢失模块树，参数也就成了摆设。
/// </remarks>
public class XiHanModuleHelperLoggingTests
{
    /// <summary>
    /// 日志记录器为空时照常完成发现
    /// </summary>
    /// <remarks>反例：logger 是可选参数，缺省时不能因为恢复了日志写入而抛空引用。</remarks>
    [Fact]
    public void FindAllModuleTypes_WhenLoggerNull_StillDiscoversModules()
    {
        var modules = XiHanModuleHelper.FindAllModuleTypes(typeof(MhlRootModule), null);

        Assert.Equal(3, modules.Count);
        Assert.Equal(typeof(MhlRootModule), modules[0]);
    }

    /// <summary>
    /// 起始类型不是模块时不写任何节点日志
    /// </summary>
    /// <remarks>边界：类型校验发生在节点行构造之前，失败时只应留下开头那条提示。</remarks>
    [Fact]
    public void FindAllModuleTypes_WhenStartupTypeIsNotModule_LogsOnlyHeader()
    {
        var logger = new CoreRecordingLogger();

        Assert.Throws<ArgumentException>(() => XiHanModuleHelper.FindAllModuleTypes(typeof(string), logger));

        Assert.Equal("加载曦寒模块:", Assert.Single(logger.Entries).Message);
    }
}

/// <summary>
/// 日志用例的叶子模块
/// </summary>
internal class MhlLeafModule : XiHanModule;

/// <summary>
/// 日志用例的中间模块
/// </summary>
[DependsOn(typeof(MhlLeafModule))]
internal class MhlMiddleModule : XiHanModule;

/// <summary>
/// 日志用例的根模块，与叶子模块构成菱形依赖
/// </summary>
[DependsOn(typeof(MhlMiddleModule), typeof(MhlLeafModule))]
internal class MhlRootModule : XiHanModule;
