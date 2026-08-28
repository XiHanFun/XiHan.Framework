// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Serilog.Core;
using Serilog.Events;
using XiHan.Framework.Logging.Tests.Fakes;

namespace XiHan.Framework.Logging.Tests;

/// <summary>
/// 曦寒日志配置构建器测试
/// </summary>
/// <remarks>
/// 构建器对外是一串流式方法，光断言「不抛异常」没有价值；
/// 这里统一挂一个内存接收器把配置真正跑起来，用流经管道的日志事件反证最小级别、级别重写与扩充器确实生效。
/// 控制台与文件两组 WriteTo 方法会在配置期就建出真实 sink（其中文件 sink 直接落盘到程序目录），
/// 不适合在单元测试里触发，另行说明未覆盖。
/// </remarks>
public class XiHanLoggerConfigurationBuilderTests
{
    private const string SourceContextPropertyName = "SourceContext";

    /// <summary>
    /// 流式方法一律返回同一个构建器实例
    /// </summary>
    [Fact]
    public void FluentMethods_ReturnSameBuilderInstance()
    {
        var builder = new XiHanLoggerConfigurationBuilder();

        Assert.Same(builder, builder.MinimumLevel(LogEventLevel.Debug));
        Assert.Same(builder, builder.MinimumLevelDefault());
        Assert.Same(builder, builder.Override("Foo", LogEventLevel.Error));
        Assert.Same(builder, builder.OverrideDefault());
        Assert.Same(builder, builder.EnrichWithProperty("K", "V"));
        Assert.Same(builder, builder.EnrichWithPropertyDefault());
        Assert.Same(builder, builder.EnrichFromLogContext());
        Assert.Same(builder, builder.EnrichFromLogContextDefault());
    }

    /// <summary>
    /// 多次构建返回同一份底层配置
    /// </summary>
    /// <remarks>
    /// 构建器包裹的是同一个 Serilog 配置对象，若每次 Build 都新建一份，
    /// 之前所有流式设置都会静默丢失。
    /// </remarks>
    [Fact]
    public void Build_ReturnsSameUnderlyingConfiguration()
    {
        var builder = new XiHanLoggerConfigurationBuilder();

        Assert.Same(builder.Build(), builder.Build());
    }

    /// <summary>
    /// 最小级别之下的事件被丢弃
    /// </summary>
    [Fact]
    public void MinimumLevel_DropsEventsBelowThreshold()
    {
        var sink = new CollectingSink();
        var builder = new XiHanLoggerConfigurationBuilder().MinimumLevel(LogEventLevel.Warning);

        using (var logger = BuildLogger(builder, sink))
        {
            logger.Debug("dropped-debug");
            logger.Information("dropped-info");
            logger.Warning("kept-warning");
            logger.Error("kept-error");
        }

        Assert.Collection(
            sink.Events,
            evt => Assert.Equal(LogEventLevel.Warning, evt.Level),
            evt => Assert.Equal(LogEventLevel.Error, evt.Level));
    }

    /// <summary>
    /// 默认最小级别放行信息级
    /// </summary>
    /// <remarks>
    /// 只断言信息级放行这一条。紧随其后的 Information 会无条件覆盖 #if DEBUG 里设的 Debug，
    /// 该分支实际不生效（已列入疑似缺陷），在裁决前不把调试级的放行与否固化进测试。
    /// </remarks>
    [Fact]
    public void MinimumLevelDefault_KeepsInformationEnabled()
    {
        var sink = new CollectingSink();
        var builder = new XiHanLoggerConfigurationBuilder().MinimumLevelDefault();

        using (var logger = BuildLogger(builder, sink))
        {
            logger.Information("kept-info");
        }

        var evt = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Information, evt.Level);
    }

    /// <summary>
    /// 级别重写只抬高匹配来源的门槛
    /// </summary>
    [Fact]
    public void Override_RaisesThresholdForMatchingSourceOnly()
    {
        var sink = new CollectingSink();
        var builder = new XiHanLoggerConfigurationBuilder().Override("Noisy", LogEventLevel.Error);

        using (var logger = BuildLogger(builder, sink))
        {
            logger.ForContext(SourceContextPropertyName, "Noisy.Component").Information("dropped");
            logger.ForContext(SourceContextPropertyName, "Noisy.Component").Error("kept-error");
            logger.ForContext(SourceContextPropertyName, "Quiet.Component").Information("kept-info");
        }

        Assert.Equal(2, sink.Events.Count);
        Assert.Contains(sink.Events, evt => evt.MessageTemplate.Text == "kept-error");
        Assert.Contains(sink.Events, evt => evt.MessageTemplate.Text == "kept-info");
    }

    /// <summary>
    /// 默认重写压制框架自身的信息级噪声
    /// </summary>
    [Theory]
    [InlineData("Microsoft.Extensions.Hosting")]
    [InlineData("System.Net.Http")]
    public void OverrideDefault_SuppressesFrameworkInformationEvents(string sourceContext)
    {
        var sink = new CollectingSink();
        var builder = new XiHanLoggerConfigurationBuilder().OverrideDefault();

        using (var logger = BuildLogger(builder, sink))
        {
            logger.ForContext(SourceContextPropertyName, sourceContext).Information("dropped");
            logger.ForContext(SourceContextPropertyName, sourceContext).Warning("kept");
        }

        var evt = Assert.Single(sink.Events);
        Assert.Equal("kept", evt.MessageTemplate.Text);
    }

    /// <summary>
    /// 固定属性挂到每一条事件上
    /// </summary>
    [Fact]
    public void EnrichWithProperty_AttachesPropertyToEveryEvent()
    {
        var sink = new CollectingSink();
        var builder = new XiHanLoggerConfigurationBuilder().EnrichWithProperty("Tenant", "t1");

        using (var logger = BuildLogger(builder, sink))
        {
            logger.Information("a");
            logger.Information("b");
        }

        Assert.Equal(2, sink.Events.Count);
        foreach (var evt in sink.Events)
        {
            Assert.True(evt.Properties.TryGetValue("Tenant", out var value));
            Assert.Equal("t1", Assert.IsType<ScalarValue>(value).Value);
        }
    }

    /// <summary>
    /// 默认扩充挂上应用名与版本两个属性
    /// </summary>
    [Fact]
    public void EnrichWithPropertyDefault_AttachesApplicationAndVersion()
    {
        var sink = new CollectingSink();
        var builder = new XiHanLoggerConfigurationBuilder().EnrichWithPropertyDefault();

        using (var logger = BuildLogger(builder, sink))
        {
            logger.Information("a");
        }

        var evt = Assert.Single(sink.Events);
        Assert.True(evt.Properties.ContainsKey("Application"));
        Assert.True(evt.Properties.ContainsKey("Version"));
    }

    /// <summary>
    /// 环境上下文里的属性被带进事件
    /// </summary>
    [Fact]
    public void EnrichFromLogContext_PicksUpAmbientProperty()
    {
        var sink = new CollectingSink();
        var builder = new XiHanLoggerConfigurationBuilder().EnrichFromLogContext();

        using (var logger = BuildLogger(builder, sink))
        {
            using (Serilog.Context.LogContext.PushProperty("RequestId", "r1"))
            {
                logger.Information("in-context");
            }

            logger.Information("out-of-context");
        }

        Assert.Equal(2, sink.Events.Count);
        Assert.True(sink.Events[0].Properties.ContainsKey("RequestId"));
        Assert.False(sink.Events[1].Properties.ContainsKey("RequestId"));
    }

    /// <summary>
    /// 未启用环境上下文扩充时不带入环境属性
    /// </summary>
    [Fact]
    public void Build_WithoutLogContextEnricher_IgnoresAmbientProperty()
    {
        var sink = new CollectingSink();
        var builder = new XiHanLoggerConfigurationBuilder();

        using (var logger = BuildLogger(builder, sink))
        {
            using (Serilog.Context.LogContext.PushProperty("RequestId", "r1"))
            {
                logger.Information("in-context");
            }
        }

        var evt = Assert.Single(sink.Events);
        Assert.False(evt.Properties.ContainsKey("RequestId"));
    }

    private static Logger BuildLogger(XiHanLoggerConfigurationBuilder builder, CollectingSink sink)
    {
        var configuration = builder.Build();
        configuration.WriteTo.Sink(sink);
        return configuration.CreateLogger();
    }
}
