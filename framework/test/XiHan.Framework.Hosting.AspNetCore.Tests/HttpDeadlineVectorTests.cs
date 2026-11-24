// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Hosting.AspNetCore.Tests;

/// <summary>
/// 回放 <c>http-deadline.json</c>：HTTP 载体读出的截止时间
/// </summary>
/// <remarks>
/// <c>context/http-deadline.json</c>：请求头 <c>xihan-deadline</c> 与 <c>grpc-timeout</c> 到截止时间（RFC-0003 规则 8、<c>spec/protocol/http.md</c>「上下文映射」与 <c>spec/protocol/grpc.md</c>，ADR-0276、ADR-0278）。gRPC、MCP 的 Streamable HTTP 与认证方案 <c>XiHan</c> 都经 <see cref="HttpInvocation.CreateContext(HttpContext, IServiceProvider)"/> 这一个读取点，回放它就覆盖了四处
/// </remarks>
public sealed class HttpDeadlineVectorTests
{
    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly DeadlineVectors _vectors = SpecVectors.Read<DeadlineVectors>("context", "http-deadline.json");

    [Fact]
    public void CreateContext_ReadsTheDeadlineOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Cases);
        using var services = new ServiceCollection().AddSingleton<TimeProvider>(new FixedClock(Utc(_vectors.Now))).BuildServiceProvider();
        var failures = new List<string>();
        foreach (var vector in _vectors.Cases)
        {
            var actual = DeadlineOf(vector.Headers, services);
            DateTimeOffset? expected = vector.Deadline is null ? null : Utc(vector.Deadline);
            if (actual != expected)
            {
                failures.Add($"{vector.Name}: read '{Describe(actual)}', the vector has '{Describe(expected)}'");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheDeadline_IsUniversal_WhateverOffsetTheHeaderCarries()
    {
        using var services = new ServiceCollection().BuildServiceProvider();

        var deadline = DeadlineOf(new Dictionary<string, string[]> { [HttpInvocation.DeadlineHeader] = ["2026-11-18T11:25:00.5+08:00"] }, services);

        Assert.Equal(new DateTimeOffset(2026, 11, 18, 3, 25, 0, 500, TimeSpan.Zero), deadline);
        Assert.Equal(TimeSpan.Zero, deadline!.Value.Offset);
    }

    [Fact]
    public void TheVectors_CoverTheTextsTheLenientParserUsedToRead()
    {
        // 这几条在改用规则 8 的文法之前都读得出（ADR-0276 背景第 2 条）；向量换了写法后不能一条都不剩
        var unreadable = _vectors.Cases
            .Where(static vector => vector.Deadline is null && vector.Headers.Count == 1 && vector.Headers.ContainsKey(HttpInvocation.DeadlineHeader))
            .SelectMany(static vector => vector.Headers[HttpInvocation.DeadlineHeader].Length == 1 ? vector.Headers[HttpInvocation.DeadlineHeader] : [])
            .ToList();

        Assert.Contains("2026-11-18 03:25:00Z", unreadable);
        Assert.Contains("2026-11-18T03:25:00", unreadable);
        Assert.Contains("2026-11-18", unreadable);
        Assert.Contains("2026-11-18T03:25:00+0800", unreadable);
        Assert.Contains(" 2026-11-18T03:25:00Z", unreadable);
        Assert.Contains("Wed, 18 Nov 2026 03:25:00 GMT", unreadable);
    }

    /// <summary>
    /// 以给定的请求头建调用上下文，取它的截止时间
    /// </summary>
    /// <param name="headers">请求头：名字到值的列表，一个头出现几次就有几项</param>
    /// <param name="services">请求的服务提供器，时钟取自它</param>
    private static DateTimeOffset? DeadlineOf(IReadOnlyDictionary<string, string[]> headers, IServiceProvider services)
    {
        var http = new DefaultHttpContext { RequestServices = services };
        foreach (var (name, values) in headers)
        {
            http.Request.Headers[name] = new StringValues(values);
        }

        return HttpInvocation.CreateContext(http, services).Deadline;
    }

    /// <summary>
    /// 向量里精确到纳秒的 UTC 时刻
    /// </summary>
    /// <remarks>
    /// 小数可以没有；比 100 纳秒更细的部分截断，与规范对实现时钟精度的要求一致
    /// </remarks>
    private static DateTimeOffset Utc(string text)
    {
        var whole = DateTime.ParseExact(text[..19], "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        var ticks = text.Length > 20 ? long.Parse(text.AsSpan(20, 7), NumberStyles.None, CultureInfo.InvariantCulture) : 0;
        return new DateTimeOffset(whole.Ticks + ticks, TimeSpan.Zero);
    }

    /// <summary>
    /// 失败信息里的截止时间
    /// </summary>
    private static string Describe(DateTimeOffset? deadline)
        => deadline?.ToString("O", CultureInfo.InvariantCulture) ?? "none";

    /// <summary>
    /// 停在一个时刻的时钟：<c>grpc-timeout</c> 相对它换算
    /// </summary>
    /// <param name="now">当前时刻</param>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="Now">宿主时钟的当前时刻</param>
    /// <param name="Cases">用例</param>
    private sealed record DeadlineVectors(string Now, DeadlineCase[] Cases);

    /// <summary>
    /// 一个用例
    /// </summary>
    /// <param name="Name">名字</param>
    /// <param name="Headers">请求头：名字到值的列表</param>
    /// <param name="Deadline">期望的截止时间（UTC，九位小数）；<see langword="null"/> 表示没有</param>
    private sealed record DeadlineCase(string Name, Dictionary<string, string[]> Headers, string? Deadline);
}
