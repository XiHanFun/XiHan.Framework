// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Localization.Database.Translations;
using XiHan.Framework.Localization.InMemory;
using XiHan.Framework.Persistence.InMemory;
using XiHan.Framework.Primitives;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Localization.Database.Tests;

/// <summary>
/// 健康项 <c>localization.database.translations</c>：宿主进入服务状态前为 Started（不影响就绪），首次加载成功后 Ready 且 <see cref="MessageTranslationLoader.LastError"/> 为空。
/// </summary>
public sealed class DatabaseLocalizationHealthTests
{
    [Fact]
    public async Task Health_IsStartedBeforeTheFirstLoad_ThenReady()
    {
        await using var host = await TestHost.StartAsync(
            static b => b.UseFeature<InMemoryPersistenceFeature>().UseFeature<InMemoryLocalizationFeature>().UseFeature<DatabaseLocalizationFeature>(),
            [new("XiHan:Localization:Database:RefreshIntervalSeconds", "0")]);
        var loader = host.Services.GetRequiredService<MessageTranslationLoader>();

        // 首次加载在宿主进入服务状态后由后台循环完成：等它跑完
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (loader.LastLoadedAt is null)
        {
            Assert.True(DateTime.UtcNow < deadline, "The translations were not loaded in time.");
            var pending = Assert.Single(host.App.Health().Contributions, static c => c.Name == DatabaseLocalizationFeature.HealthName);
            Assert.NotEqual(LifecycleState.Degraded, pending.State);
            await Task.Delay(10);
        }

        var health = host.App.Health();
        var status = Assert.Single(health.Contributions, static c => c.Name == DatabaseLocalizationFeature.HealthName);
        Assert.Equal(LifecycleState.Ready, status.State);
        Assert.Null(status.Error);
        Assert.Null(loader.LastError);
        Assert.Equal(LifecycleState.Ready, health.State);
    }
}
