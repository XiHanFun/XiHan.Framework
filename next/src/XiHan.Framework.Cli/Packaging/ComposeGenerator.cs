// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 生成本地联调的 Compose 文件：应用服务与每个绑定一个依赖服务。
/// </summary>
internal static class ComposeGenerator
{
    /// <summary>
    /// 生成 <c>compose.yaml</c>。
    /// </summary>
    /// <remarks>
    /// <paramref name="buildContext"/> 与 <paramref name="dockerfile"/> 是相对 compose 文件与构建上下文的路径。
    /// </remarks>
    public static PackOutput Generate(AppManifest manifest, string buildContext, string dockerfile)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var yaml = new YamlText()
            .Line(0, "# 由 xihan pack 生成：本地联调用，绑定的依赖以容器提供。")
            .Pair(0, "name", manifest.Name)
            .Line(0, "services:")
            .Line(2, "app:")
            .Line(4, "build:")
            .Pair(6, "context", buildContext)
            .Pair(6, "dockerfile", dockerfile)
            .Pair(4, "image", manifest.ImageReference)
            .Line(4, "ports:")
            .Line(6, "- " + YamlText.Scalar($"{manifest.Ports.Http}:{manifest.Ports.Http}"));
        if (manifest.Ports.Grpc is { } grpc)
        {
            yaml.Line(6, "- " + YamlText.Scalar($"{grpc}:{grpc}"));
        }

        yaml.Pair(4, "stop_grace_period", manifest.Shutdown.GraceSeconds.ToString(CultureInfo.InvariantCulture) + "s")
            .Line(4, "healthcheck:")
            // chiseled 镜像里没有 shell 与 curl：调用 Dockerfile 随镜像构建的就绪探针
            .Line(6, "test: " + HealthProbe.ComposeTest(manifest))
            .Pair(6, "interval", manifest.Probes.PeriodSeconds.ToString(CultureInfo.InvariantCulture) + "s")
            .Pair(6, "timeout", manifest.Probes.TimeoutSeconds.ToString(CultureInfo.InvariantCulture) + "s")
            .Pair(6, "start_period", manifest.Probes.StartupSeconds.ToString(CultureInfo.InvariantCulture) + "s")
            .Line(4, "environment:");
        foreach (var (binding, settings) in manifest.Bindings.OrderBy(static b => b.Key, StringComparer.Ordinal))
        {
            yaml.Pair(6, "XiHan__Bindings__" + binding + "__type", settings.Type)
                .Pair(6, "XiHan__Bindings__" + binding + "__value", BindingKind.Find(settings.Type)!.ComposeValue(binding, manifest.Name));
        }

        foreach (var (key, value) in manifest.Environment.OrderBy(static e => e.Key, StringComparer.Ordinal))
        {
            yaml.Pair(6, key, value);
        }

        if (manifest.Bindings.Count > 0)
        {
            yaml.Line(4, "depends_on:");
            foreach (var binding in manifest.Bindings.Keys.OrderBy(static k => k, StringComparer.Ordinal))
            {
                yaml.Line(6, binding + ":").Pair(8, "condition", "service_healthy");
            }
        }

        var volumes = new List<string>();
        foreach (var (binding, settings) in manifest.Bindings.OrderBy(static b => b.Key, StringComparer.Ordinal))
        {
            var kind = BindingKind.Find(settings.Type)!;
            yaml.Line(2, binding + ":").Pair(4, "image", kind.Image);
            switch (kind.Type)
            {
                case "connection:relational":
                    yaml.Line(4, "environment:")
                        .Pair(6, "POSTGRES_USER", "xihan")
                        .Pair(6, "POSTGRES_PASSWORD", "xihan")
                        .Pair(6, "POSTGRES_DB", manifest.Name)
                        .Line(4, "healthcheck:")
                        .Line(6, "test: [\"CMD-SHELL\", \"pg_isready -U xihan\"]")
                        .Pair(6, "interval", "5s")
                        .Line(4, "volumes:")
                        .Line(6, "- " + binding + "-data:/var/lib/postgresql/data");
                    volumes.Add(binding + "-data");
                    break;
                case "cache:distributed":
                    yaml.Line(4, "healthcheck:")
                        .Line(6, "test: [\"CMD\", \"redis-cli\", \"ping\"]")
                        .Pair(6, "interval", "5s");
                    break;
                case "queue":
                    yaml.Line(4, "healthcheck:")
                        .Line(6, "test: [\"CMD\", \"rabbitmq-diagnostics\", \"-q\", \"ping\"]")
                        .Pair(6, "interval", "10s");
                    break;
                case "stream":
                    // 单节点 KRaft：同一进程兼任 broker 与 controller；一旦给了 KAFKA_* 变量镜像就不再用自带的配置文件，必需项都要写全。
                    // 对外地址写服务名，Compose 网络里的应用拿到的元数据才指向可达的主机，而不是镜像缺省的 localhost
                    yaml.Line(4, "environment:")
                        .Pair(6, "KAFKA_NODE_ID", "1")
                        .Pair(6, "KAFKA_PROCESS_ROLES", "broker,controller")
                        .Pair(6, "KAFKA_LISTENERS", "PLAINTEXT://:9092,CONTROLLER://:9093")
                        .Pair(6, "KAFKA_ADVERTISED_LISTENERS", $"PLAINTEXT://{binding}:{kind.Port}")
                        .Pair(6, "KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
                        .Pair(6, "KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT")
                        .Pair(6, "KAFKA_CONTROLLER_QUORUM_VOTERS", "1@localhost:9093")
                        .Pair(6, "KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
                        .Pair(6, "KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
                        .Pair(6, "KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
                        .Pair(6, "KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
                        .Line(4, "healthcheck:")
                        .Line(6, $"test: [\"CMD\", \"/opt/kafka/bin/kafka-broker-api-versions.sh\", \"--bootstrap-server\", \"localhost:{kind.Port}\"]")
                        .Pair(6, "interval", "10s")
                        .Pair(6, "start_period", "30s");
                    break;
                case "configuration:remote":
                    // standalone 用内嵌 Derby 存配置，挂卷才能在重建容器后保留；JVM 启动慢，启动期内的失败不计入重试
                    yaml.Line(4, "environment:")
                        .Pair(6, "MODE", "standalone")
                        .Line(4, "healthcheck:")
                        .Line(6, $"test: [\"CMD\", \"curl\", \"-fs\", \"http://localhost:{kind.Port}/nacos/v1/console/health/readiness\"]")
                        .Pair(6, "interval", "10s")
                        .Pair(6, "start_period", "60s")
                        .Line(4, "volumes:")
                        .Line(6, "- " + binding + "-data:/home/nacos/data");
                    volumes.Add(binding + "-data");
                    break;
                default:
                    yaml.Line(4, "command: [\"server\", \"/data\"]")
                        .Line(4, "environment:")
                        .Pair(6, "MINIO_ROOT_USER", "xihan")
                        .Pair(6, "MINIO_ROOT_PASSWORD", "xihanxihan")
                        .Line(4, "healthcheck:")
                        .Line(6, "test: [\"CMD\", \"mc\", \"ready\", \"local\"]")
                        .Pair(6, "interval", "5s")
                        .Line(4, "volumes:")
                        .Line(6, "- " + binding + "-data:/data");
                    volumes.Add(binding + "-data");
                    break;
            }
        }

        if (volumes.Count > 0)
        {
            yaml.Line(0, "volumes:");
            foreach (var volume in volumes)
            {
                yaml.Line(2, volume + ": {}");
            }
        }

        return new PackOutput("compose.yaml", yaml.ToString());
    }
}
