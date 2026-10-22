// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 生成多阶段 Dockerfile：SDK 镜像发布，chiseled 运行镜像，非 root。
/// </summary>
internal static class DockerfileGenerator
{
    /// <summary>
    /// 生成 <c>Dockerfile</c> 与 <c>.dockerignore</c>。
    /// </summary>
    public static IEnumerable<PackOutput> Generate(AppManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        yield return new PackOutput("Dockerfile", Dockerfile(manifest));
        yield return new PackOutput(".dockerignore", "**/bin/\n**/obj/\n**/deploy/\n.git/\n");
    }

    /// <summary>
    /// Dockerfile 文本。
    /// </summary>
    private static string Dockerfile(AppManifest manifest)
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

        builder.Append("WORKDIR /src\n");
        builder.Append("COPY . .\n");
        builder.Append(CultureInfo.InvariantCulture, $"RUN dotnet publish \"{manifest.Project.Replace('\\', '/')}\" --configuration Release --arch $TARGETARCH --output /app/publish");
        builder.Append(manifest.Image.Aot ? " --self-contained true -p:PublishAot=true\n" : " --self-contained false\n");
        builder.Append('\n');
        builder.Append(manifest.Image.Aot ? "FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled AS runtime\n" : "FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime\n");
        builder.Append("WORKDIR /app\n");
        builder.Append("COPY --from=build /app/publish .\n");
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
