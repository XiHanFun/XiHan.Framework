// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using XiHan.Framework.Traffic.GrayRouting.Models;
using XiHan.Framework.Web.Gateway.Constants;
using XiHan.Framework.Web.Gateway.Helpers;

namespace XiHan.Framework.Web.Gateway.Tests;

/// <summary>
/// 网关上下文帮助类测试
/// </summary>
/// <remarks>
/// 这组扩展方法是业务代码读取网关中间件产出的唯一入口，全部是纯函数：
/// 只从 HttpContext.Items 里取值并做类型转换，不产生副作用。
/// 关键契约是「键不存在 / 类型不对时安全返回 null，而不是抛异常」，
/// 否则未接入网关中间件的场景（如单元测试、内部调用）会直接崩。
/// </remarks>
public class GatewayContextHelperTests
{
    /// <summary>
    /// 未注入 TraceId 时返回空
    /// </summary>
    [Fact]
    public void GetTraceId_WhenItemMissing_ReturnsNull()
    {
        var context = new DefaultHttpContext();

        Assert.Null(context.GetTraceId());
    }

    /// <summary>
    /// 已注入 TraceId 时原样返回
    /// </summary>
    [Fact]
    public void GetTraceId_WhenItemPresent_ReturnsStoredValue()
    {
        var context = new DefaultHttpContext();
        context.Items[GatewayConstants.TraceIdKey] = "trace-1";

        Assert.Equal("trace-1", context.GetTraceId());
    }

    /// <summary>
    /// 非字符串的 TraceId 走 ToString 转换
    /// </summary>
    /// <remarks>
    /// Items 是弱类型字典，任何模块都可能塞进非字符串值，这里确认不会因强转失败而抛异常。
    /// </remarks>
    [Fact]
    public void GetTraceId_WhenItemIsNotString_ReturnsToStringResult()
    {
        var context = new DefaultHttpContext();
        context.Items[GatewayConstants.TraceIdKey] = 12345L;

        Assert.Equal("12345", context.GetTraceId());
    }

    /// <summary>
    /// 未注入灰度决策时返回空
    /// </summary>
    [Fact]
    public void GetGrayDecision_WhenItemMissing_ReturnsNull()
    {
        var context = new DefaultHttpContext();

        Assert.Null(context.GetGrayDecision());
    }

    /// <summary>
    /// 灰度决策键上放了别的类型时返回空而不是抛异常
    /// </summary>
    [Fact]
    public void GetGrayDecision_WhenItemIsOtherType_ReturnsNull()
    {
        var context = new DefaultHttpContext();
        context.Items[GatewayConstants.GrayDecisionKey] = "not-a-decision";

        Assert.Null(context.GetGrayDecision());
    }

    /// <summary>
    /// 已注入灰度决策时返回同一个实例
    /// </summary>
    [Fact]
    public void GetGrayDecision_WhenDecisionStored_ReturnsSameInstance()
    {
        var decision = GrayDecision.Gray("v2", "rule-1");
        var context = new DefaultHttpContext();
        context.Items[GatewayConstants.GrayDecisionKey] = decision;

        Assert.Same(decision, context.GetGrayDecision());
    }

    /// <summary>
    /// 未经过灰度中间件时不算灰度请求
    /// </summary>
    [Fact]
    public void IsGrayRequest_WhenNoDecision_ReturnsFalse()
    {
        var context = new DefaultHttpContext();

        Assert.False(context.IsGrayRequest());
    }

    /// <summary>
    /// 决策未命中灰度时不算灰度请求
    /// </summary>
    [Fact]
    public void IsGrayRequest_WhenDecisionNotGray_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        context.Items[GatewayConstants.GrayDecisionKey] = GrayDecision.NotGray("未命中任何灰度规则");

        Assert.False(context.IsGrayRequest());
    }

    /// <summary>
    /// 决策命中灰度时算灰度请求
    /// </summary>
    [Fact]
    public void IsGrayRequest_WhenDecisionIsGray_ReturnsTrue()
    {
        var context = new DefaultHttpContext();
        context.Items[GatewayConstants.GrayDecisionKey] = GrayDecision.Gray("v2", "rule-1");

        Assert.True(context.IsGrayRequest());
    }

    /// <summary>
    /// 灰度决策键上放了别的类型时不算灰度请求
    /// </summary>
    [Fact]
    public void IsGrayRequest_WhenItemIsOtherType_ReturnsFalse()
    {
        var context = new DefaultHttpContext();
        context.Items[GatewayConstants.GrayDecisionKey] = 42;

        Assert.False(context.IsGrayRequest());
    }

    /// <summary>
    /// 未注入灰度决策时目标版本为空
    /// </summary>
    [Fact]
    public void GetTargetVersion_WhenNoDecision_ReturnsNull()
    {
        var context = new DefaultHttpContext();

        Assert.Null(context.GetTargetVersion());
    }

    /// <summary>
    /// 未命中灰度的决策没有目标版本
    /// </summary>
    [Fact]
    public void GetTargetVersion_WhenDecisionNotGray_ReturnsNull()
    {
        var context = new DefaultHttpContext();
        context.Items[GatewayConstants.GrayDecisionKey] = GrayDecision.NotGray("未命中任何灰度规则");

        Assert.Null(context.GetTargetVersion());
    }

    /// <summary>
    /// 命中灰度时返回决策里的目标版本
    /// </summary>
    [Fact]
    public void GetTargetVersion_WhenDecisionIsGray_ReturnsTargetVersion()
    {
        var context = new DefaultHttpContext();
        context.Items[GatewayConstants.GrayDecisionKey] = GrayDecision.Gray("v2", "rule-1");

        Assert.Equal("v2", context.GetTargetVersion());
    }

    /// <summary>
    /// TraceId 与灰度决策各用各的键，互不干扰
    /// </summary>
    [Fact]
    public void Helpers_ReadIndependentItemKeys()
    {
        var decision = GrayDecision.Gray("v2", "rule-1");
        var context = new DefaultHttpContext();
        context.Items[GatewayConstants.TraceIdKey] = "trace-1";
        context.Items[GatewayConstants.GrayDecisionKey] = decision;

        Assert.Equal("trace-1", context.GetTraceId());
        Assert.Same(decision, context.GetGrayDecision());
        Assert.True(context.IsGrayRequest());
        Assert.Equal("v2", context.GetTargetVersion());
    }
}
