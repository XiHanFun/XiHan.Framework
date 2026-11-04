// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 生成多阶段 Dockerfile：SDK 镜像发布，chiseled 运行镜像，非 root
/// </summary>
/// <remarks>
/// 构建阶段另外编译就绪探针（<see cref="HealthProbe"/>），复制进运行镜像供 Compose 健康检查使用
/// </remarks>
internal static class DockerfileGenerator
{
    /// <summary>
    /// 生成 <c>Dockerfile</c> 与 <c>Dockerfile.dockerignore</c>
    /// </summary>
    /// <remarks>
    /// <paramref name="projectInContext"/> 是项目文件相对构建上下文的路径
    /// </remarks>
    public static IEnumerable<PackOutput> Generate(AppManifest manifest, string projectInContext)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(projectInContext);
        yield return new PackOutput("Dockerfile", Dockerfile(manifest, projectInContext));
        // BuildKit 读与 Dockerfile 同目录的 <Dockerfile>.dockerignore，产物目录不必是构建上下文根
        yield return new PackOutput("Dockerfile.dockerignore", "**/bin/\n**/obj/\n**/deploy/\n.git/\n");
    }

    /// <summary>
    /// Dockerfile 文本
    /// </summary>
    private static string Dockerfile(AppManifest manifest, string projectInContext)
    {
        var builder = new StringBuilder();
        builder.Append("# syntax=docker/dockerfile:1\n");
        builder.Append("# 由 xihan pack 从 xihan.app.json 生成；重新生成会覆盖本文件。\n");
        builder.Append("FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build\n");
        builder.Append("ARG TARGETARCH\n");
        if (manifest.Image.Aot)
        {
            builder.Append("RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev && rm -rf /var/lib/apt/lists/*\n");
        }

        // 就绪探针先于应用源码构建：它不随应用变化，这几层可以复用缓存；heredoc 的定界符加引号，内容不做变量展开
        builder.Append("# 就绪探针：chiseled 运行镜像里没有 shell、curl 与 wget，Compose 的 healthcheck 用它探测 /health/ready\n");
        builder.Append(CultureInfo.InvariantCulture, $"COPY <<\"EOF\" {HealthProbe.SourceDirectory}/{HealthProbe.Name}.csproj\n").Append(HealthProbe.Project(manifest.Image.Aot)).Append("EOF\n");
        builder.Append(CultureInfo.InvariantCulture, $"COPY <<\"EOF\" {HealthProbe.SourceDirectory}/Program.cs\n").Append(HealthProbe.Program).Append("EOF\n");
        builder.Append(CultureInfo.InvariantCulture, $"RUN dotnet publish {HealthProbe.SourceDirectory}/{HealthProbe.Name}.csproj --configuration Release");
        builder.Append(manifest.Image.Aot ? " --arch $TARGETARCH" : string.Empty);
        builder.Append(CultureInfo.InvariantCulture, $" --output {HealthProbe.PublishDirectory}\n");
        builder.Append("WORKDIR /src\n");
        builder.Append("COPY . .\n");
        builder.Append(CultureInfo.InvariantCulture, $"RUN dotnet publish \"{projectInContext}\" --configuration Release --arch $TARGETARCH --output /app/publish");
        builder.Append(manifest.Image.Aot ? " --self-contained true -p:PublishAot=true\n" : " --self-contained false\n");
        builder.Append('\n');
        builder.Append(manifest.Image.Aot ? "FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS runtime\n" : "FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime\n");
        builder.Append("WORKDIR /app\n");
        builder.Append("COPY --from=build /app/publish .\n");
        builder.Append(manifest.Image.Aot
            ? $"COPY --from=build {HealthProbe.PublishDirectory}/{HealthProbe.Name} {HealthProbe.Directory}/{HealthProbe.Name}\n"
            : $"COPY --from=build {HealthProbe.PublishDirectory} {HealthProbe.Directory}\n");
        builder.Append(CultureInfo.InvariantCulture, $"ENV Kestrel__Endpoints__Http__Url=http://0.0.0.0:{manifest.Ports.Http}\n");
        if (manifest.Ports.Grpc is { } grpc)
        {
            builder.Append(CultureInfo.InvariantCulture, $"ENV Kestrel__Endpoints__Grpc__Url=http://0.0.0.0:{grpc} Kestrel__Endpoints__Grpc__Protocols=Http2\n");
        }

        builder.Append(CultureInfo.InvariantCulture, $"ENV XiHan__Kernel__StopTimeoutSeconds={manifest.Shutdown.GraceSeconds}\n");
        builder.Append(CultureInfo.InvariantCulture, $"EXPOSE {manifest.Ports.Http}");
        if (manifest.Ports.Grpc is { } exposed)
        {
            builder.Append(CultureInfo.InvariantCulture, $" {exposed}");
        }

        builder.Append('\n');
        builder.Append("USER $APP_UID\n");
        builder.Append(manifest.Image.Aot
            ? $"ENTRYPOINT [\"./{manifest.AssemblyName}\"]\n"
            : $"ENTRYPOINT [\"dotnet\", \"{manifest.AssemblyName}.dll\"]\n");
        return builder.ToString();
    }
}
