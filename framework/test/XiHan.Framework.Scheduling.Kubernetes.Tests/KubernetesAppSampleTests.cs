// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.InMemory;

namespace XiHan.Framework.Scheduling.Kubernetes.Tests;

/// <summary>
/// 示例 samples/KubernetesApp 的调度配置要落到引擎实际读取的键上
/// </summary>
/// <remarks>
/// 轮询间隔与租约存活时间属于 <c>XiHan:Scheduling</c>（引擎），<c>XiHan:Scheduling:InMemory</c> 只有内存存储自己的 <c>MaxRuns</c>
/// </remarks>
public sealed class KubernetesAppSampleTests
{
    [Fact]
    public void Appsettings_SetsPollIntervalAndLeaseTimeToLive_OnTheEngine()
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(FindNextRoot(), "samples", "KubernetesApp", "appsettings.json"), optional: false, reloadOnChange: false).Build();

        var options = SchedulerOptions.FromConfiguration(configuration);

        // README：删除持有者 Pod 后由另一个 Pod 接管，依赖这里的 15 秒租约与 1 秒轮询
        Assert.Equal(TimeSpan.FromSeconds(15), options.LeaseTimeToLive);
        Assert.Equal(TimeSpan.FromSeconds(1), options.PollInterval);
        Assert.Empty(configuration.GetSection(InMemorySchedulingOptions.ConfigurationSection).GetChildren());
    }

    /// <summary>
    /// 从测试输出目录向上找 <c>next/</c>（含解决方案文件的目录）
    /// </summary>
    private static string FindNextRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "XiHan.Framework.Next.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The next/ workspace root was not found above the test output directory.");
    }
}
