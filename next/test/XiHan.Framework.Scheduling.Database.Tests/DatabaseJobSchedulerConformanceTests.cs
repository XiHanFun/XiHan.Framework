// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Conformance;

namespace XiHan.Framework.Scheduling.Database.Tests;

/// <summary>
/// 数据库调度特性对 SQLite 通过调度器套件。
/// </summary>
public sealed class DatabaseJobSchedulerConformanceTests : JobSchedulerConformance
{
    /// <inheritdoc />
    protected override async Task<JobSchedulerHost> StartAsync(Action<IServiceCollection> configure, IEnumerable<KeyValuePair<string, string?>>? configuration = null)
    {
        var settings = new List<KeyValuePair<string, string?>>
        {
            new($"{SchedulerOptions.ConfigurationSection}:PollIntervalSeconds", "0.05"),
            new($"{SchedulerOptions.ConfigurationSection}:HousekeepingIntervalSeconds", "0.2"),
        };
        if (configuration is not null)
        {
            settings.AddRange(configuration);
        }

        var database = new SqliteDatabase();
        var host = await database.StartHostAsync(settings, configure);
        return new JobSchedulerHost(host.Services.GetRequiredService<IJobScheduler>(), database);
    }
}
