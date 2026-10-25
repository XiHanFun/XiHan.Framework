// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Tests.Packaging;

/// <summary>
/// 临时目录里的一份清单与一个空项目文件；释放时删除目录。
/// </summary>
internal sealed class ManifestFixture : IDisposable
{
    /// <summary>
    /// 一份用到全部字段的清单。
    /// </summary>
    public const string FullManifest = """
        {
          "name": "orders",
          "project": "Orders.csproj",
          "profile": "full",
          "image": { "repository": "ghcr.io/acme/orders", "tag": "1.2.0", "aot": false },
          "ports": { "http": 8080, "grpc": 8081 },
          "bindings": {
            "db": { "type": "connection:relational", "secret": "orders-db" },
            "cache": { "type": "cache:distributed" },
            "queue": { "type": "queue" },
            "blob": { "type": "blob" }
          },
          "config": { "mountPath": "/etc/xihan", "files": ["appsettings.Production.json", "missing.json"] },
          "probes": { "startupSeconds": 45, "periodSeconds": 10, "timeoutSeconds": 3 },
          "shutdown": { "graceSeconds": 20 },
          "resources": { "requests": { "cpu": "250m", "memory": "256Mi" }, "limits": { "cpu": "1", "memory": "512Mi" } },
          "replicas": 2,
          "scaling": { "min": 2, "max": 6, "cpuPercent": 60 },
          "leader": { "lease": "orders-scheduler" },
          "environment": { "ASPNETCORE_ENVIRONMENT": "Production", "OTEL_SERVICE_NAME": "orders" }
        }
        """;

    /// <summary>
    /// 创建目录、项目文件与清单。
    /// </summary>
    public ManifestFixture(string manifest = FullManifest, string project = "Orders.csproj")
    {
        Directory = Path.Combine(Path.GetTempPath(), "xihan-cli-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(Path.Combine(Directory, project), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        File.WriteAllText(Path.Combine(Directory, "appsettings.Production.json"), "{\n  \"Logging\": { \"LogLevel\": { \"Default\": \"Warning\" } }\n}\n");
        ManifestPath = Path.Combine(Directory, "xihan.app.json");
        File.WriteAllText(ManifestPath, manifest);
    }

    /// <summary>
    /// 临时目录。
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// 清单路径。
    /// </summary>
    public string ManifestPath { get; }

    /// <summary>
    /// 默认输出目录。
    /// </summary>
    public string OutputDirectory => Path.Combine(Directory, "deploy");

    /// <summary>
    /// 读输出目录里的文件。
    /// </summary>
    public string Output(string relative) => File.ReadAllText(Path.Combine(OutputDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));

    /// <inheritdoc />
    public void Dispose()
    {
        if (System.IO.Directory.Exists(Directory))
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}
