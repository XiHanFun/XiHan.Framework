// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using XiHan.Framework.Observability.Abstractions;

namespace XiHan.Framework.Observability.Host.Tests;

/// <summary>
/// 回放 <c>host-usage.json</c>：使用率、速率与已用量的算法
/// </summary>
/// <remarks>
/// 规范 RFC-0023「运行信息」的「契约」与「采样」（ADR-0056、ADR-0277）：百分比在 0 到 100 之间、不取整；计数变小、总增量为零或任一次没有读到时该项为空，不以 0 顶替。用例的结果都是二进制浮点能精确表示的数，按相等比较。采样器的求差是私有的，这里给它计数可控的读取路径与可推进的时钟，采两次样。文件里 <c>linux</c> 一节（各项由 <c>/proc</c> 与 <c>/sys</c> 的文本读出的取法，ADR-0286）由 <see cref="LinuxHostReadingVectorTests"/> 回放。向量是规范的一部分，这里只回放，不另存一份
/// </remarks>
public sealed class HostUsageVectorTests
{
    /// <summary>
    /// 用例里网卡的 ID
    /// </summary>
    private const string Nic = "eth0";

    /// <summary>
    /// 向量文件的内容
    /// </summary>
    private static readonly HostUsageVectors _vectors = Load();

    [Fact]
    public void SampleInterval_HasTheDefaultAndTheRangeOfTheVectors()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(_vectors.SampleInterval.DefaultMs), new HostObservabilityOptions().SampleInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(_vectors.SampleInterval.MaxMs), HostObservabilityOptions.MaxSampleInterval);

        // 赋值与读配置（以秒计的 SampleIntervalSeconds）两条路的取值范围必须一样
        Assert.NotEmpty(_vectors.SampleInterval.Valid);
        var rejected = _vectors.SampleInterval.Valid.Where(static ms => !AcceptedWhenAssigned(ms) || !AcceptedFromConfiguration(ms)).ToList();
        Assert.True(rejected.Count == 0, "Valid sample intervals (ms) that were rejected: " + string.Join(", ", rejected));

        Assert.NotEmpty(_vectors.SampleInterval.Invalid);
        var accepted = _vectors.SampleInterval.Invalid.Where(static ms => AcceptedWhenAssigned(ms) || AcceptedFromConfiguration(ms)).ToList();
        Assert.True(accepted.Count == 0, "Invalid sample intervals (ms) that were accepted: " + string.Join(", ", accepted));
    }

    [Fact]
    public void Sampler_ComputesTheCpuUsageOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Cpu);
        var failures = new List<string>();
        foreach (var vector in _vectors.Cpu)
        {
            var actual = SampleTwice(
                logicalCores: 1,
                elapsed: TimeSpan.FromSeconds(1),
                first: platform => platform.Cpu = TimesOf(vector.Before),
                second: platform => platform.Cpu = TimesOf(vector.After)).CpuUsagePercent;
            if (actual != vector.UsagePercent)
            {
                failures.Add($"{vector.Name}: computed {Describe(actual)}, the vector has {Describe(vector.UsagePercent)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Sampler_ComputesTheProcessUsageOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Process);
        var failures = new List<string>();
        foreach (var vector in _vectors.Process)
        {
            var actual = SampleTwice(
                vector.Cores,
                TimeSpan.FromMilliseconds(vector.ElapsedMs),
                first: platform => platform.ProcessCpu = TimeSpan.FromMilliseconds(vector.ProcessCpuBeforeMs),
                second: platform => platform.ProcessCpu = TimeSpan.FromMilliseconds(vector.ProcessCpuAfterMs)).ProcessCpuUsagePercent;
            if (actual != vector.UsagePercent)
            {
                failures.Add($"{vector.Name}: computed {Describe(actual)}, the vector has {Describe(vector.UsagePercent)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Sampler_ComputesTheNetworkRateOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Network);
        var failures = new List<string>();
        foreach (var vector in _vectors.Network)
        {
            var elapsed = TimeSpan.FromMilliseconds(vector.ElapsedMs);

            // 收与发各算各的：用例的计数放在收的一侧时，发的一侧是没有流量的计数（速率为 0），反过来也一样；一侧为空不连累另一侧
            var receiving = SampleTwice(
                logicalCores: 1,
                elapsed,
                first: platform => platform.Network = [new NetworkCounters(Nic, vector.Before, 0)],
                second: platform => platform.Network = [new NetworkCounters(Nic, vector.After, 0)]).NetworkRate(Nic);
            var sending = SampleTwice(
                logicalCores: 1,
                elapsed,
                first: platform => platform.Network = [new NetworkCounters(Nic, 0, vector.Before)],
                second: platform => platform.Network = [new NetworkCounters(Nic, 0, vector.After)]).NetworkRate(Nic);
            if (receiving != (vector.BytesPerSecond, 0d) || sending != (0d, vector.BytesPerSecond))
            {
                failures.Add($"{vector.Name}: computed {Describe(receiving.Receive)} as the receive rate and {Describe(sending.Send)} as the send rate (the idle sides were {Describe(receiving.Send)} and {Describe(sending.Receive)}), the vector has {Describe(vector.BytesPerSecond)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void MemoryInfo_ComputesUsedAndUsageOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Memory);
        var failures = new List<string>();
        foreach (var vector in _vectors.Memory)
        {
            var memory = new MemoryInfo { TotalBytes = vector.TotalBytes, AvailableBytes = vector.AvailableBytes };
            if (memory.UsedBytes != vector.UsedBytes || memory.UsagePercent != vector.UsagePercent)
            {
                failures.Add($"{vector.Name}: computed {Describe(memory.UsedBytes)} used and {Describe(memory.UsagePercent)} percent, the vector has {Describe(vector.UsedBytes)} and {Describe(vector.UsagePercent)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void DiskInfo_ComputesUsedAndUsageOfEveryCase()
    {
        Assert.NotEmpty(_vectors.Disks);
        var failures = new List<string>();
        foreach (var vector in _vectors.Disks)
        {
            var disk = new DiskInfo { Name = "/", Type = DriveType.Fixed, TotalBytes = vector.TotalBytes, FreeBytes = vector.FreeBytes, AvailableBytes = vector.FreeBytes };
            if (disk.UsedBytes != vector.UsedBytes || disk.UsagePercent != vector.UsagePercent)
            {
                failures.Add($"{vector.Name}: computed {Describe(disk.UsedBytes)} used and {Describe(disk.UsagePercent)} percent, the vector has {Describe(vector.UsedBytes)} and {Describe(vector.UsagePercent)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheVectors_CoverTheEmptyAndTheClampedValues()
    {
        // 防止向量换了写法后某一类一条都没核对还照样通过。采样间隔：正好到上限的要收，零与只超一毫秒的要拒
        Assert.Contains(_vectors.SampleInterval.MaxMs, _vectors.SampleInterval.Valid);
        Assert.Contains(0L, _vectors.SampleInterval.Invalid);
        Assert.Contains(_vectors.SampleInterval.MaxMs + 1, _vectors.SampleInterval.Invalid);

        // 系统 CPU：前后各有一次没读到、总增量为零、总计数变小、空闲计数变小都为空；空闲增量超过总增量限制在 0；有不取整的结果
        Assert.Contains(_vectors.Cpu, static vector => vector.Before is null && vector.UsagePercent is null);
        Assert.Contains(_vectors.Cpu, static vector => vector.After is null && vector.UsagePercent is null);
        Assert.Contains(_vectors.Cpu, static vector => vector is { Before: { } before, After: { } after, UsagePercent: null } && after.Total == before.Total);
        Assert.Contains(_vectors.Cpu, static vector => vector is { Before: { } before, After: { } after, UsagePercent: null } && after.Total < before.Total);
        Assert.Contains(_vectors.Cpu, static vector => vector is { Before: { } before, After: { } after, UsagePercent: null } && after.Total > before.Total && after.Idle < before.Idle);
        Assert.Contains(_vectors.Cpu, static vector => vector is { Before: { } before, After: { } after, UsagePercent: 0d } && after.Total > before.Total && after.Idle - before.Idle > after.Total - before.Total);
        Assert.Contains(_vectors.Cpu, static vector => vector.UsagePercent is { } percent && percent != Math.Round(percent));

        // 进程 CPU：算出来超过 100 的限制在 100，处理器时间变小为空
        Assert.Contains(_vectors.Process, static vector => vector.UsagePercent == 100d && vector.ProcessCpuAfterMs - vector.ProcessCpuBeforeMs > vector.ElapsedMs * vector.Cores);
        Assert.Contains(_vectors.Process, static vector => vector.UsagePercent is null && vector.ProcessCpuAfterMs < vector.ProcessCpuBeforeMs);
        Assert.Contains(_vectors.Process, static vector => vector.Cores > 1 && vector.UsagePercent is > 0d and < 100d);

        // 网卡：前后各有一次没读到、计数变小都为空
        Assert.Contains(_vectors.Network, static vector => vector.Before is null && vector.BytesPerSecond is null);
        Assert.Contains(_vectors.Network, static vector => vector.After is null && vector.BytesPerSecond is null);
        Assert.Contains(_vectors.Network, static vector => vector.After < vector.Before && vector.BytesPerSecond is null);
        Assert.Contains(_vectors.Network, static vector => vector.ElapsedMs != 1000 && vector.BytesPerSecond is > 0d);

        // 内存与磁盘：读不到的总量或可用量让已用为空，总量为零时使用率为空而已用不为空
        Assert.Contains(_vectors.Memory, static vector => vector.TotalBytes is null && vector.UsedBytes is null);
        Assert.Contains(_vectors.Memory, static vector => vector.AvailableBytes is null && vector.UsedBytes is null);
        Assert.Contains(_vectors.Memory, static vector => vector is { TotalBytes: 0, UsedBytes: not null, UsagePercent: null });
        Assert.Contains(_vectors.Disks, static vector => vector is { TotalBytes: 0, UsagePercent: null });
    }

    /// <summary>
    /// 采两次样，返回采过之后的采样器
    /// </summary>
    /// <param name="logicalCores">逻辑核心数</param>
    /// <param name="elapsed">两次采样之间经过的时间</param>
    /// <param name="first">设定第一次读到的计数</param>
    /// <param name="second">设定第二次读到的计数</param>
    private static HostSampler SampleTwice(int logicalCores, TimeSpan elapsed, Action<FakeHostPlatform> first, Action<FakeHostPlatform> second)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero));
        var platform = new FakeHostPlatform();
        var sampler = new HostSampler(platform, HostObservabilityOptions.MaxSampleInterval, clock, NullLogger.Instance, logicalCores);

        first(platform);
        sampler.Sample();
        clock.Advance(elapsed);
        second(platform);
        sampler.Sample();
        return sampler;
    }

    /// <summary>
    /// 用例里的一次 CPU 时间读数
    /// </summary>
    /// <returns>没有读到为 <see langword="null"/></returns>
    private static CpuTimes? TimesOf(CpuReading? reading)
        => reading is null ? null : new CpuTimes(reading.Idle, reading.Total);

    /// <summary>
    /// 把这个间隔赋给 <see cref="HostObservabilityOptions.SampleInterval"/> 是否被接受
    /// </summary>
    /// <param name="milliseconds">间隔的毫秒数</param>
    private static bool AcceptedWhenAssigned(long milliseconds)
    {
        var interval = TimeSpan.FromMilliseconds(milliseconds);
        try
        {
            return new HostObservabilityOptions { SampleInterval = interval }.SampleInterval == interval;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>
    /// 把这个间隔写在配置的 <c>SampleIntervalSeconds</c> 是否被接受
    /// </summary>
    /// <param name="milliseconds">间隔的毫秒数</param>
    private static bool AcceptedFromConfiguration(long milliseconds)
    {
        // 配置以秒计：毫秒数除以一千，用 decimal 写出来不带舍入误差（1 毫秒是 0.001）
        var seconds = (milliseconds / 1000m).ToString(CultureInfo.InvariantCulture);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal) { [$"{HostObservabilityOptions.ConfigurationSection}:SampleIntervalSeconds"] = seconds })
            .Build();
        try
        {
            var interval = HostObservabilityOptions.FromConfiguration(configuration).SampleInterval;
            return interval > TimeSpan.Zero && interval <= HostObservabilityOptions.MaxSampleInterval;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 失败信息里的数
    /// </summary>
    private static string Describe(double? value)
        => value?.ToString("R", CultureInfo.InvariantCulture) ?? "null";

    /// <summary>
    /// 失败信息里的字节数
    /// </summary>
    private static string Describe(long? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? "null";

    /// <summary>
    /// 读向量文件
    /// </summary>
    private static HostUsageVectors Load()
    {
        var path = Path.Combine(LocateRoot(), "spec", "conformance", "vectors", "observability", "host-usage.json");
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<HostUsageVectors>(stream, JsonSerializerOptions.Web) ?? throw new InvalidOperationException($"Vector file '{path}' is empty.");
    }

    /// <summary>
    /// 从测试输出目录向上查找框架工作区根（以 <c>XiHan.Framework.slnx</c> 为标记）
    /// </summary>
    private static string LocateRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The framework workspace root (the directory of 'XiHan.Framework.slnx') was not found above the test output directory.");
    }

    /// <summary>
    /// 向量文件的对象模型
    /// </summary>
    /// <param name="SampleInterval">采样间隔的缺省值与取值范围</param>
    /// <param name="Cpu">系统 CPU 使用率的用例</param>
    /// <param name="Process">进程 CPU 使用率的用例</param>
    /// <param name="Network">网卡速率的用例</param>
    /// <param name="Memory">内存的已用与使用率的用例</param>
    /// <param name="Disks">磁盘的已用与使用率的用例</param>
    private sealed record HostUsageVectors(SampleIntervalCases SampleInterval, CpuCase[] Cpu, ProcessCase[] Process, NetworkCase[] Network, MemoryCase[] Memory, DiskCase[] Disks);

    /// <summary>
    /// 采样间隔的用例
    /// </summary>
    /// <param name="DefaultMs">缺省值（毫秒）</param>
    /// <param name="MaxMs">上限（毫秒）</param>
    /// <param name="Valid">范围内的毫秒数</param>
    /// <param name="Invalid">不在范围内的毫秒数</param>
    private sealed record SampleIntervalCases(long DefaultMs, long MaxMs, long[] Valid, long[] Invalid);

    /// <summary>
    /// 一个系统 CPU 使用率用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Before">前一次读到的累计时间；<see langword="null"/> 表示没有读到</param>
    /// <param name="After">这一次读到的累计时间；<see langword="null"/> 表示没有读到</param>
    /// <param name="UsagePercent">使用率；<see langword="null"/> 表示为空</param>
    private sealed record CpuCase(string Name, CpuReading? Before, CpuReading? After, double? UsagePercent);

    /// <summary>
    /// 一次读到的累计 CPU 时间
    /// </summary>
    /// <param name="Idle">空闲时间</param>
    /// <param name="Total">总时间</param>
    private sealed record CpuReading(ulong Idle, ulong Total);

    /// <summary>
    /// 一个进程 CPU 使用率用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="Cores">逻辑核心数</param>
    /// <param name="ElapsedMs">两次采样之间经过的毫秒数</param>
    /// <param name="ProcessCpuBeforeMs">前一次读到的进程累计处理器时间（毫秒）</param>
    /// <param name="ProcessCpuAfterMs">这一次读到的进程累计处理器时间（毫秒）</param>
    /// <param name="UsagePercent">使用率；<see langword="null"/> 表示为空</param>
    private sealed record ProcessCase(string Name, int Cores, long ElapsedMs, long ProcessCpuBeforeMs, long ProcessCpuAfterMs, double? UsagePercent);

    /// <summary>
    /// 一个网卡速率用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="ElapsedMs">两次采样之间经过的毫秒数</param>
    /// <param name="Before">前一次读到的累计字节数；<see langword="null"/> 表示没有读到</param>
    /// <param name="After">这一次读到的累计字节数；<see langword="null"/> 表示没有读到</param>
    /// <param name="BytesPerSecond">速率（字节每秒）；<see langword="null"/> 表示为空</param>
    private sealed record NetworkCase(string Name, long ElapsedMs, long? Before, long? After, double? BytesPerSecond);

    /// <summary>
    /// 一个内存用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="TotalBytes">总量；<see langword="null"/> 表示读不到</param>
    /// <param name="AvailableBytes">可用量；<see langword="null"/> 表示读不到</param>
    /// <param name="UsedBytes">已用量</param>
    /// <param name="UsagePercent">使用率</param>
    private sealed record MemoryCase(string Name, long? TotalBytes, long? AvailableBytes, long? UsedBytes, double? UsagePercent);

    /// <summary>
    /// 一个磁盘用例
    /// </summary>
    /// <param name="Name">用例的说明</param>
    /// <param name="TotalBytes">总量</param>
    /// <param name="FreeBytes">空闲量</param>
    /// <param name="UsedBytes">已用量</param>
    /// <param name="UsagePercent">使用率</param>
    private sealed record DiskCase(string Name, long TotalBytes, long FreeBytes, long UsedBytes, double? UsagePercent);
}
