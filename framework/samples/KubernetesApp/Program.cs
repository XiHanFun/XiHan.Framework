// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using KubernetesApp;
using Microsoft.Extensions.Options;
using KubernetesApp.Endpoints;
using KubernetesApp.Heartbeat;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Profiles.Full;
using XiHan.Framework.Protocols.Http.Routing;
using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Abstractions.Schedulers;
using XiHan.Framework.Scheduling.Kubernetes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, SampleJsonContext.Default));
builder.Services.Configure<BannerOptions>(builder.Configuration.GetSection(BannerOptions.Section));

// 在 Pod 里（API Server 的环境变量存在）用 Lease 做领导选举；本地运行退回 Full 组合自带的进程内选举
var inCluster = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST") is not null;

// AddXiHan 先装载 XIHAN_CONFIG_PATH（ConfigMap 挂载点）里的 JSON 文件并监听变化
builder.AddXiHan(xihan =>
{
    xihan.UseFullProfile().UseFeature<HeartbeatFeature>();
    if (inCluster)
    {
        xihan.UseFeature<KubernetesSchedulingFeature>();
    }
});

var app = builder.Build();

app.UseXiHan();
app.MapXiHanHealth();        // /health/startup、/health/ready、/health/live：Deployment 的三个探针
app.MapXiHanOperations();

app.MapGet("/", (IOptionsMonitor<BannerOptions> banner) => new BannerView(banner.CurrentValue.Banner, Environment.MachineName));

// ConfigMap 热更新：改掉挂载文件里的 Sample:Banner，IOptionsMonitor 跟着变，无需重启
app.MapGet("/config", (IOptionsMonitor<BannerOptions> banner, IConfiguration configuration) =>
    new ConfigView(banner.CurrentValue.Banner, configuration["XiHan:Kernel:StopTimeoutSeconds"], Environment.GetEnvironmentVariable(ConfigurationDirectory.Variable)));

// 领导选举：只有持有 Lease 的 Pod 按计划运行心跳作业，Ticks 只在领导上增长
app.MapGet("/leader", (JobSchedulerEngine scheduler, ILeaderElection election, HeartbeatCounter counter) =>
    new LeaderView(election is KubernetesLeaderElection kubernetes ? kubernetes.Holder : Environment.MachineName, election.GetType().Name, scheduler.IsLeader, counter.Ticks));

app.Run();
