// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Packaging;

namespace XiHan.Framework.Cli.Tests.Packaging;

/// <summary>
/// 应用清单的读取、缺省值与校验。
/// </summary>
public sealed class AppManifestReaderTests
{
    [Fact]
    public void Read_FullManifest_PopulatesEveryField()
    {
        using var fixture = new ManifestFixture();
        var manifest = AppManifestReader.Read(fixture.ManifestPath).ValueOrThrow();

        Assert.Equal(("orders", "Orders.csproj", "full", "Orders"), (manifest.Name, manifest.Project, manifest.Profile, manifest.AssemblyName));
        Assert.Equal("ghcr.io/acme/orders:1.2.0", manifest.ImageReference);
        Assert.Equal((8080, 8081), (manifest.Ports.Http, manifest.Ports.Grpc));
        Assert.Equal(["blob", "cache", "db", "queue"], manifest.Bindings.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("orders-db", manifest.Bindings["db"].Secret);
        Assert.Equal(["appsettings.Production.json", "missing.json"], manifest.Config.Files);
        Assert.Equal((45, 10, 3, 5), (manifest.Probes.StartupSeconds, manifest.Probes.PeriodSeconds, manifest.Probes.TimeoutSeconds, manifest.Probes.StartupFailureThreshold));
        Assert.Equal((20, 25), (manifest.Shutdown.GraceSeconds, manifest.Shutdown.TerminationGracePeriodSeconds));
        Assert.Equal("512Mi", manifest.Resources?.Limits?.Memory);
        Assert.Equal((2, 2, 6, 60), (manifest.Replicas, manifest.Scaling!.Min, manifest.Scaling.Max, manifest.Scaling.CpuPercent));
        Assert.Equal("orders-scheduler", manifest.Leader?.Lease);
        Assert.Equal("Production", manifest.Environment["ASPNETCORE_ENVIRONMENT"]);
    }

    [Fact]
    public void Read_MinimalManifest_AppliesDefaults()
    {
        using var fixture = new ManifestFixture("""{ "name": "simple", "project": "Orders.csproj" }""");
        var manifest = AppManifestReader.Read(fixture.ManifestPath).ValueOrThrow();

        Assert.Equal(("standard", "simple:latest", 8080, 1, 30, 60), (manifest.Profile, manifest.ImageReference, manifest.Ports.Http, manifest.Replicas, manifest.Shutdown.GraceSeconds, manifest.Probes.StartupSeconds));
        Assert.Null(manifest.Ports.Grpc);
        Assert.Empty(manifest.Bindings);
        Assert.Equal("/etc/xihan", manifest.Config.MountPath);
        Assert.Null(manifest.Scaling);
        Assert.Null(manifest.Leader);
        Assert.False(manifest.Image.Aot);
    }

    [Fact]
    public void Read_InvalidManifest_ListsEveryProblem()
    {
        using var fixture = new ManifestFixture("""
            {
              "name": "Orders-",
              "project": "Missing.csproj",
              "profile": "huge",
              "build": { "context": "nowhere" },
              "ports": { "http": 70000, "grpc": 70000 },
              "bindings": { "Db": { "type": "mystery" } },
              "config": { "mountPath": "etc", "files": ["a/b.json"] },
              "probes": { "startupSeconds": 5, "periodSeconds": 10 },
              "shutdown": { "graceSeconds": 0 },
              "resources": { "limits": { "cpu": "lots" } },
              "replicas": 0,
              "scaling": { "min": 3, "max": 1, "cpuPercent": 150 },
              "leader": { "lease": "Lease" },
              "environment": { "1BAD": "x" }
            }
            """);
        var failure = AppManifestReader.Read(fixture.ManifestPath);

        Assert.Equal(CliErrors.ManifestInvalid, failure.Error?.Code);
        var reason = (string)failure.Error!.Details!["reason"]!;
        foreach (var expected in new[] { "name 'Orders-'", "project 'Missing.csproj'", "profile 'huge'", "build.context 'nowhere'", "ports.http", "ports.grpc", "binding name 'Db'", "unknown type 'mystery'", "config.mountPath", "config.files entry 'a/b.json'", "probes:", "shutdown.graceSeconds", "resources.limits.cpu 'lots'", "replicas", "scaling:", "leader.lease 'Lease'", "environment key '1BAD'" })
        {
            Assert.Contains(expected, reason, StringComparison.Ordinal);
        }

        File.WriteAllText(fixture.ManifestPath, "{ not json");
        Assert.Equal(CliErrors.ManifestInvalid, AppManifestReader.Read(fixture.ManifestPath).Error?.Code);
    }
}
