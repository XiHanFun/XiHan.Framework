// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Cli.Packaging;

namespace XiHan.Framework.Cli.Tests;

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
        Assert.Contains("**/bin/", files[".dockerignore"], StringComparison.Ordinal);
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
        Assert.Contains("XiHan__Bindings__blob__value: http://blob:9000", compose, StringComparison.Ordinal);
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
        Assert.Contains(".WithEnvironment(\"XiHan__Bindings__blob__value\", blob.GetEndpoint(\"api\"))", aspire, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"OTEL_SERVICE_NAME\", \"orders\")", aspire, StringComparison.Ordinal);
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
