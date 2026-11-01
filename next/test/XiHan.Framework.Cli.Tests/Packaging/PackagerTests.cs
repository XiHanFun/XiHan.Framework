// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Packaging;

namespace XiHan.Framework.Cli.Tests.Packaging;

/// <summary>
/// 各生成器：Dockerfile、Kustomize、Compose 与 Aspire 的关键内容与条件产物。
/// </summary>
public sealed class PackagerTests
{
    /// <summary>
    /// 从夹具读清单并生成全部产物。
    /// </summary>
    private static (AppManifest Manifest, Dictionary<string, string> Files) Generate(ManifestFixture fixture, PackKinds kinds = PackKinds.All)
    {
        var manifest = AppManifestReader.Read(fixture.ManifestPath).ValueOrThrow();
        var outputs = Packager.Generate(manifest, fixture.Directory, fixture.OutputDirectory, kinds);
        return (manifest, outputs.ToDictionary(static o => o.Path, static o => o.Content, StringComparer.Ordinal));
    }

    [Fact]
    public void Dockerfile_IsMultiStageChiseledAndNonRoot()
    {
        using var fixture = new ManifestFixture();
        var (_, files) = Generate(fixture, PackKinds.Dockerfile);
        var dockerfile = files["Dockerfile"];

        Assert.Contains("FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build", dockerfile, StringComparison.Ordinal);
        Assert.Contains("RUN dotnet publish \"Orders.csproj\" --configuration Release --arch $TARGETARCH --output /app/publish --self-contained false", dockerfile, StringComparison.Ordinal);
        Assert.Contains("FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime", dockerfile, StringComparison.Ordinal);
        Assert.Contains("ENV Kestrel__Endpoints__Http__Url=http://0.0.0.0:8080", dockerfile, StringComparison.Ordinal);
        Assert.Contains("ENV Kestrel__Endpoints__Grpc__Url=http://0.0.0.0:8081 Kestrel__Endpoints__Grpc__Protocols=Http2", dockerfile, StringComparison.Ordinal);
        Assert.Contains("ENV XiHan__Kernel__StopTimeoutSeconds=20", dockerfile, StringComparison.Ordinal);
        Assert.Contains("EXPOSE 8080 8081", dockerfile, StringComparison.Ordinal);
        Assert.Contains("USER $APP_UID", dockerfile, StringComparison.Ordinal);
        Assert.Contains("ENTRYPOINT [\"dotnet\", \"Orders.dll\"]", dockerfile, StringComparison.Ordinal);
        Assert.DoesNotContain("clang", dockerfile, StringComparison.Ordinal);
        Assert.Contains("**/bin/", files["Dockerfile.dockerignore"], StringComparison.Ordinal);

        // 就绪探针：构建阶段从内联源码编译（框架依赖），整个发布目录复制进运行镜像
        Assert.Contains("COPY <<\"EOF\" /xihan/healthprobe-src/XiHanHealthProbe.csproj\n", dockerfile, StringComparison.Ordinal);
        Assert.Contains("COPY <<\"EOF\" /xihan/healthprobe-src/Program.cs\n", dockerfile, StringComparison.Ordinal);
        Assert.Contains("RUN dotnet publish /xihan/healthprobe-src/XiHanHealthProbe.csproj --configuration Release --output /xihan/healthprobe-src/publish\n", dockerfile, StringComparison.Ordinal);
        Assert.Contains("COPY --from=build /xihan/healthprobe-src/publish /xihan/healthprobe\n", dockerfile, StringComparison.Ordinal);
        Assert.DoesNotContain("<PublishAot>", dockerfile, StringComparison.Ordinal);
        Assert.True(dockerfile.IndexOf("XiHanHealthProbe.csproj --configuration", StringComparison.Ordinal) < dockerfile.IndexOf("COPY . .", StringComparison.Ordinal), "the probe is built before the application sources so its layers stay cached");
    }

    [Fact]
    public void Dockerfile_WithBuildContext_PublishesTheProjectRelativeToTheContext()
    {
        using var fixture = new ManifestFixture("""{ "name": "simple", "project": "Orders.csproj", "build": { "context": ".." } }""");
        var (_, files) = Generate(fixture, PackKinds.Dockerfile | PackKinds.Compose);
        var folder = Path.GetFileName(fixture.Directory);

        Assert.Contains($"RUN dotnet publish \"{folder}/Orders.csproj\"", files["Dockerfile"], StringComparison.Ordinal);
        Assert.Contains($"context: ../..\n      dockerfile: {folder}/deploy/Dockerfile\n", files["compose.yaml"], StringComparison.Ordinal);
    }

    [Fact]
    public void Dockerfile_Aot_UsesRuntimeDepsAndNativeEntryPoint()
    {
        using var fixture = new ManifestFixture("""{ "name": "simple", "project": "Orders.csproj", "image": { "aot": true } }""");
        var (_, files) = Generate(fixture, PackKinds.Dockerfile);
        var dockerfile = files["Dockerfile"];

        Assert.Contains("apt-get install -y --no-install-recommends clang zlib1g-dev", dockerfile, StringComparison.Ordinal);
        Assert.Contains("--self-contained true -p:PublishAot=true", dockerfile, StringComparison.Ordinal);
        Assert.Contains("FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS runtime", dockerfile, StringComparison.Ordinal);
        Assert.Contains("ENTRYPOINT [\"./Orders\"]", dockerfile, StringComparison.Ordinal);
        Assert.Contains("EXPOSE 8080\n", dockerfile, StringComparison.Ordinal);
        Assert.DoesNotContain("Grpc", dockerfile, StringComparison.Ordinal);

        // runtime-deps 里没有 dotnet：就绪探针也以 Native AOT 发布（项目里声明 PublishAot），只复制原生可执行文件
        Assert.Contains("    <PublishAot>true</PublishAot>\n  </PropertyGroup>", dockerfile, StringComparison.Ordinal);
        Assert.Contains("RUN dotnet publish /xihan/healthprobe-src/XiHanHealthProbe.csproj --configuration Release --arch $TARGETARCH --output /xihan/healthprobe-src/publish\n", dockerfile, StringComparison.Ordinal);
        Assert.Contains("COPY --from=build /xihan/healthprobe-src/publish/XiHanHealthProbe /xihan/healthprobe/XiHanHealthProbe\n", dockerfile, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_HealthCheck_ProbesTheReadyEndpointWithTheBundledProbe()
    {
        using var fixture = new ManifestFixture();
        var compose = Generate(fixture, PackKinds.Compose).Files["compose.yaml"];

        Assert.Contains("    healthcheck:\n      test: [\"CMD\", \"dotnet\", \"/xihan/healthprobe/XiHanHealthProbe.dll\", \"8080\", \"/health/ready\"]\n      interval: 10s\n      timeout: 3s\n      start_period: 45s\n", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("--version", compose, StringComparison.Ordinal);

        // AOT：运行镜像里没有 dotnet，直接执行原生探针
        using var aot = new ManifestFixture("""{ "name": "simple", "project": "Orders.csproj", "ports": { "http": 9000 }, "image": { "aot": true } }""");
        var aotCompose = Generate(aot, PackKinds.Compose).Files["compose.yaml"];
        Assert.Contains("test: [\"CMD\", \"/xihan/healthprobe/XiHanHealthProbe\", \"9000\", \"/health/ready\"]\n      interval: 10s\n      timeout: 2s\n      start_period: 60s\n", aotCompose, StringComparison.Ordinal);
        Assert.DoesNotContain("\"dotnet\"", aotCompose, StringComparison.Ordinal);
    }

    [Fact]
    public void Kustomize_MapsProbesShutdownBindingsConfigScalingAndLeader()
    {
        using var fixture = new ManifestFixture();
        var (_, files) = Generate(fixture, PackKinds.Kustomize);

        Assert.Equal(["kustomize/bindings.example.yaml", "kustomize/configmap.yaml", "kustomize/deployment.yaml", "kustomize/hpa.yaml", "kustomize/kustomization.yaml", "kustomize/pdb.yaml", "kustomize/rbac.yaml", "kustomize/service.yaml"], files.Keys.Order(StringComparer.Ordinal));
        var deployment = files["kustomize/deployment.yaml"];
        Assert.Contains("replicas: 2\n", deployment, StringComparison.Ordinal);
        Assert.Contains("serviceAccountName: orders\n", deployment, StringComparison.Ordinal);
        Assert.Contains("terminationGracePeriodSeconds: 25\n", deployment, StringComparison.Ordinal);
        Assert.Contains("image: ghcr.io/acme/orders:1.2.0\n", deployment, StringComparison.Ordinal);
        Assert.Contains("- name: XiHan__Kernel__StopTimeoutSeconds\n              value: \"20\"", deployment, StringComparison.Ordinal);
        Assert.Contains("- name: SERVICE_BINDING_ROOT\n              value: /bindings", deployment, StringComparison.Ordinal);
        Assert.Contains("- name: XIHAN_CONFIG_PATH\n              value: /etc/xihan", deployment, StringComparison.Ordinal);
        Assert.Contains("- name: XiHan__Scheduling__Kubernetes__Lease\n              value: orders-scheduler", deployment, StringComparison.Ordinal);
        Assert.Contains("fieldPath: metadata.name", deployment, StringComparison.Ordinal);
        Assert.Contains("- name: POD_NAMESPACE\n              valueFrom:\n                fieldRef:\n                  fieldPath: metadata.namespace", deployment, StringComparison.Ordinal);
        Assert.Contains("- name: DOTNET_USE_POLLING_FILE_WATCHER\n              value: \"1\"", deployment, StringComparison.Ordinal);
        Assert.Contains("- name: ASPNETCORE_ENVIRONMENT\n              value: Production", deployment, StringComparison.Ordinal);
        // 启动探针阈值 = ⌈45 / 10⌉ = 5
        Assert.Contains("startupProbe:\n            httpGet:\n              path: /health/startup\n              port: http\n            periodSeconds: 10\n            timeoutSeconds: 3\n            failureThreshold: 5", deployment, StringComparison.Ordinal);
        Assert.Contains("path: /health/ready", deployment, StringComparison.Ordinal);
        Assert.Contains("path: /health/live", deployment, StringComparison.Ordinal);
        Assert.Contains("requests:\n              cpu: 250m\n              memory: 256Mi", deployment, StringComparison.Ordinal);
        Assert.Contains("limits:\n              cpu: \"1\"\n              memory: 512Mi", deployment, StringComparison.Ordinal);
        Assert.Contains("readOnlyRootFilesystem: true", deployment, StringComparison.Ordinal);
        Assert.Contains("mountPath: /bindings/db\n              readOnly: true", deployment, StringComparison.Ordinal);
        Assert.Contains("secretName: orders-db", deployment, StringComparison.Ordinal);
        Assert.Contains("secretName: orders-cache", deployment, StringComparison.Ordinal);
        Assert.Contains("name: orders-config", deployment, StringComparison.Ordinal);

        Assert.Contains("- name: grpc\n      port: 8081\n      targetPort: grpc", files["kustomize/service.yaml"], StringComparison.Ordinal);
        var configMap = files["kustomize/configmap.yaml"];
        Assert.Contains("appsettings.Production.json: |\n    {\n      \"Logging\"", configMap, StringComparison.Ordinal);
        Assert.Contains("missing.json: |\n    {}", configMap, StringComparison.Ordinal);
        Assert.Contains("minReplicas: 2\n  maxReplicas: 6", files["kustomize/hpa.yaml"], StringComparison.Ordinal);
        Assert.Contains("averageUtilization: 60", files["kustomize/hpa.yaml"], StringComparison.Ordinal);
        Assert.Contains("minAvailable: 1", files["kustomize/pdb.yaml"], StringComparison.Ordinal);
        Assert.Contains("resourceNames: [orders-scheduler]", files["kustomize/rbac.yaml"], StringComparison.Ordinal);
        Assert.Contains("kind: RoleBinding", files["kustomize/rbac.yaml"], StringComparison.Ordinal);
        Assert.Contains("type: connection:relational", files["kustomize/bindings.example.yaml"], StringComparison.Ordinal);
        // 凭据属性以注释列出，与 Provider 读取的绑定属性一致；关系型数据库的凭据在连接串里，没有单独的键
        var secrets = files["kustomize/bindings.example.yaml"];
        Assert.Contains("uri: \"<cache connection string>\"\n  # username: <cache username>\n  # password: <cache password>\n", secrets, StringComparison.Ordinal);
        Assert.Contains("uri: \"<queue connection string>\"\n  # username: <queue username>\n  # password: <queue password>\n", secrets, StringComparison.Ordinal);
        Assert.Contains("uri: \"<blob connection string>\"\n  # accessKey: <blob accessKey>\n  # secretKey: <blob secretKey>\n", secrets, StringComparison.Ordinal);
        Assert.DoesNotContain("<db username>", secrets, StringComparison.Ordinal);
        var kustomization = files["kustomize/kustomization.yaml"];
        Assert.Contains("resources:\n  - deployment.yaml\n  - service.yaml\n  - configmap.yaml\n  - hpa.yaml\n  - pdb.yaml\n  - rbac.yaml\n", kustomization, StringComparison.Ordinal);
        Assert.DoesNotContain("bindings.example.yaml", kustomization, StringComparison.Ordinal);
        Assert.Contains("- name: ghcr.io/acme/orders\n    newTag: \"1.2.0\"", kustomization, StringComparison.Ordinal);
    }

    [Fact]
    public void Kustomize_MinimalManifest_SkipsOptionalResources()
    {
        using var fixture = new ManifestFixture("""{ "name": "simple", "project": "Orders.csproj" }""");
        var (_, files) = Generate(fixture, PackKinds.Kustomize);

        Assert.Equal(["kustomize/deployment.yaml", "kustomize/kustomization.yaml", "kustomize/service.yaml"], files.Keys.Order(StringComparer.Ordinal));
        var deployment = files["kustomize/deployment.yaml"];
        Assert.Contains("replicas: 1\n", deployment, StringComparison.Ordinal);
        Assert.Contains("terminationGracePeriodSeconds: 35\n", deployment, StringComparison.Ordinal);
        Assert.DoesNotContain("SERVICE_BINDING_ROOT", deployment, StringComparison.Ordinal);
        Assert.DoesNotContain("serviceAccountName", deployment, StringComparison.Ordinal);
        Assert.DoesNotContain("resources:", deployment, StringComparison.Ordinal);
        Assert.Contains("failureThreshold: 6", deployment, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_AddsOneServicePerBinding_WithBindingEnvironment()
    {
        using var fixture = new ManifestFixture();
        var (_, files) = Generate(fixture, PackKinds.Compose);
        var compose = files["compose.yaml"];

        Assert.Contains("name: orders\n", compose, StringComparison.Ordinal);
        Assert.Contains("context: ..\n      dockerfile: deploy/Dockerfile\n", compose, StringComparison.Ordinal);
        Assert.Contains("- \"8080:8080\"\n      - \"8081:8081\"", compose, StringComparison.Ordinal);
        Assert.Contains("stop_grace_period: 20s", compose, StringComparison.Ordinal);
        Assert.Contains("XiHan__Bindings__db__type: connection:relational\n      XiHan__Bindings__db__value: \"Host=db;Port=5432;Database=orders;Username=xihan;Password=xihan\"", compose, StringComparison.Ordinal);
        Assert.Contains("XiHan__Bindings__cache__value: cache:6379", compose, StringComparison.Ordinal);
        Assert.Contains("XiHan__Bindings__queue__value: amqp://guest:guest@queue:5672", compose, StringComparison.Ordinal);
        Assert.Contains("XiHan__Bindings__blob__value: http://blob:9000\n      XiHan__Bindings__blob__accessKey: xihan\n      XiHan__Bindings__blob__secretKey: xihanxihan\n", compose, StringComparison.Ordinal);
        Assert.Contains("MINIO_ROOT_USER: xihan\n      MINIO_ROOT_PASSWORD: xihanxihan\n", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("XiHan__Bindings__queue__password", compose, StringComparison.Ordinal);
        Assert.Contains("depends_on:\n      blob:\n        condition: service_healthy", compose, StringComparison.Ordinal);
        Assert.Contains("  db:\n    image: postgres:17-alpine", compose, StringComparison.Ordinal);
        Assert.Contains("  cache:\n    image: redis:7-alpine", compose, StringComparison.Ordinal);
        Assert.Contains("  queue:\n    image: rabbitmq:4-alpine", compose, StringComparison.Ordinal);
        Assert.Contains("  blob:\n    image: minio/minio", compose, StringComparison.Ordinal);
        Assert.Contains("volumes:\n  blob-data: {}\n  db-data: {}", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void Aspire_RegistersProjectWithResourcesAndBindingEnvironment()
    {
        using var fixture = new ManifestFixture();
        var (_, files) = Generate(fixture, PackKinds.Aspire);
        var aspire = files["aspire/XiHanAppHost.g.cs"];

        Assert.Contains("// 需要的包：Aspire.Hosting.PostgreSQL、Aspire.Hosting.RabbitMQ、Aspire.Hosting.Redis", aspire, StringComparison.Ordinal);
        Assert.Contains("public static IResourceBuilder<ProjectResource> AddOrders(this IDistributedApplicationBuilder builder)", aspire, StringComparison.Ordinal);
        Assert.Contains("var db = builder.AddPostgres(\"db\").AddDatabase(\"orders\");", aspire, StringComparison.Ordinal);
        Assert.Contains("var cache = builder.AddRedis(\"cache\");", aspire, StringComparison.Ordinal);
        Assert.Contains("var queue = builder.AddRabbitMQ(\"queue\");", aspire, StringComparison.Ordinal);
        Assert.Contains("var blob = builder.AddContainer(\"blob\", \"minio/minio\")", aspire, StringComparison.Ordinal);
        Assert.Contains("builder.AddProject<Projects.Orders>(\"orders\")", aspire, StringComparison.Ordinal);
        Assert.Contains(".WithHttpEndpoint(port: 8080, name: \"http\")", aspire, StringComparison.Ordinal);
        Assert.Contains(".WithEndpoint(port: 8081, scheme: \"http\", name: \"grpc\")", aspire, StringComparison.Ordinal);
        Assert.Contains(".WithReplicas(2)", aspire, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"XiHan__Bindings__db__type\", \"connection:relational\")", aspire, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"XiHan__Bindings__db__value\", db)\n            .WithReference(db)", aspire, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"XiHan__Bindings__blob__value\", blob.GetEndpoint(\"api\"))\n            .WithEnvironment(\"XiHan__Bindings__blob__accessKey\", \"xihan\")\n            .WithEnvironment(\"XiHan__Bindings__blob__secretKey\", \"xihanxihan\")\n", aspire, StringComparison.Ordinal);
        Assert.Contains("        blob.WithEnvironment(\"MINIO_ROOT_USER\", \"xihan\").WithEnvironment(\"MINIO_ROOT_PASSWORD\", \"xihanxihan\");\n        var app = builder.AddProject", aspire, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"OTEL_SERVICE_NAME\", \"orders\")", aspire, StringComparison.Ordinal);
    }

    /// <summary>
    /// 用到 Kafka 与 Nacos 两种绑定的清单。
    /// </summary>
    private const string StreamAndRemoteConfigurationManifest = """
        {
          "name": "events",
          "project": "Orders.csproj",
          "bindings": {
            "stream": { "type": "stream" },
            "nacos": { "type": "configuration:remote" }
          }
        }
        """;

    [Fact]
    public void Compose_StreamAndRemoteConfiguration_RunSingleNodeKraftAndStandaloneNacos()
    {
        using var fixture = new ManifestFixture(StreamAndRemoteConfigurationManifest);
        var compose = Generate(fixture, PackKinds.Compose).Files["compose.yaml"];

        // 快照：依赖服务按绑定名排序；值的形式与 Kafka（引导地址）和 Nacos（服务地址 URL）读取绑定主值的方式一致
        Assert.Equal(
            """
            # 由 xihan pack 生成：本地联调用，绑定的依赖以容器提供。
            name: events
            services:
              app:
                build:
                  context: ..
                  dockerfile: deploy/Dockerfile
                image: events:latest
                ports:
                  - "8080:8080"
                stop_grace_period: 30s
                healthcheck:
                  test: ["CMD", "dotnet", "/xihan/healthprobe/XiHanHealthProbe.dll", "8080", "/health/ready"]
                  interval: 10s
                  timeout: 2s
                  start_period: 60s
                environment:
                  XiHan__Bindings__nacos__type: configuration:remote
                  XiHan__Bindings__nacos__value: http://nacos:8848
                  XiHan__Bindings__stream__type: stream
                  XiHan__Bindings__stream__value: stream:9092
                depends_on:
                  nacos:
                    condition: service_healthy
                  stream:
                    condition: service_healthy
              nacos:
                image: nacos/nacos-server:v2.5.1
                environment:
                  MODE: standalone
                healthcheck:
                  test: ["CMD", "curl", "-fs", "http://localhost:8848/nacos/v1/console/health/readiness"]
                  interval: 10s
                  start_period: 60s
                volumes:
                  - nacos-data:/home/nacos/data
              stream:
                image: apache/kafka:4.1.0
                environment:
                  KAFKA_NODE_ID: "1"
                  KAFKA_PROCESS_ROLES: "broker,controller"
                  KAFKA_LISTENERS: "PLAINTEXT://:9092,CONTROLLER://:9093"
                  KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://stream:9092
                  KAFKA_CONTROLLER_LISTENER_NAMES: CONTROLLER
                  KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: "CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT"
                  KAFKA_CONTROLLER_QUORUM_VOTERS: 1@localhost:9093
                  KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: "1"
                  KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: "1"
                  KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: "1"
                  KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS: "0"
                healthcheck:
                  test: ["CMD", "/opt/kafka/bin/kafka-broker-api-versions.sh", "--bootstrap-server", "localhost:9092"]
                  interval: 10s
                  start_period: 30s
            volumes:
              nacos-data: {}

            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            compose);
    }

    [Fact]
    public void Aspire_StreamAndRemoteConfiguration_AddKafkaAndAPinnedNacosContainer()
    {
        using var fixture = new ManifestFixture(StreamAndRemoteConfigurationManifest);
        var aspire = Generate(fixture, PackKinds.Aspire).Files["aspire/XiHanAppHost.g.cs"];

        Assert.Contains("// 需要的包：Aspire.Hosting.Kafka\n", aspire, StringComparison.Ordinal);
        Assert.Contains("var stream = builder.AddKafka(\"stream\");", aspire, StringComparison.Ordinal);
        Assert.Contains("var nacos = builder.AddContainer(\"nacos\", \"nacos/nacos-server\", \"v2.5.1\").WithEnvironment(\"MODE\", \"standalone\").WithHttpEndpoint(port: 8848, targetPort: 8848, name: \"http\");", aspire, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"XiHan__Bindings__nacos__type\", \"configuration:remote\")\n            .WithEnvironment(\"XiHan__Bindings__nacos__value\", nacos.GetEndpoint(\"http\"))\n", aspire, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"XiHan__Bindings__stream__type\", \"stream\")\n            .WithEnvironment(\"XiHan__Bindings__stream__value\", stream)\n            .WithReference(stream)\n", aspire, StringComparison.Ordinal);
        Assert.DoesNotContain(".WithReference(nacos)", aspire, StringComparison.Ordinal);
    }

    [Fact]
    public void Kustomize_StreamAndRemoteConfiguration_MountOneSecretPerBinding()
    {
        using var fixture = new ManifestFixture(StreamAndRemoteConfigurationManifest);
        var files = Generate(fixture, PackKinds.Kustomize).Files;

        var deployment = files["kustomize/deployment.yaml"];
        Assert.Contains("mountPath: /bindings/nacos\n              readOnly: true", deployment, StringComparison.Ordinal);
        Assert.Contains("mountPath: /bindings/stream\n              readOnly: true", deployment, StringComparison.Ordinal);
        Assert.Contains("secretName: events-nacos", deployment, StringComparison.Ordinal);
        Assert.Contains("secretName: events-stream", deployment, StringComparison.Ordinal);
        var secrets = files["kustomize/bindings.example.yaml"];
        Assert.Contains("name: events-nacos\nstringData:\n  type: configuration:remote\n", secrets, StringComparison.Ordinal);
        Assert.Contains("name: events-stream\nstringData:\n  type: stream\n", secrets, StringComparison.Ordinal);
        Assert.Contains("  # username: <nacos username>\n  # password: <nacos password>\n", secrets, StringComparison.Ordinal);
        Assert.Contains("  # security.protocol: <stream security.protocol>\n  # sasl.mechanism: <stream sasl.mechanism>\n  # sasl.username: <stream sasl.username>\n  # sasl.password: <stream sasl.password>\n", secrets, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteAsync_WritesLfUtf8FilesIntoNestedDirectories()
    {
        using var fixture = new ManifestFixture();
        var (manifest, _) = Generate(fixture);
        var outputs = Packager.Generate(manifest, fixture.Directory, fixture.OutputDirectory, PackKinds.All);
        await Packager.WriteAsync(outputs, fixture.OutputDirectory, CancellationToken.None);

        Assert.Equal(12, outputs.Count);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(fixture.OutputDirectory, "kustomize", "deployment.yaml"));
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.True(File.Exists(Path.Combine(fixture.OutputDirectory, "aspire", "XiHanAppHost.g.cs")));
    }
}
