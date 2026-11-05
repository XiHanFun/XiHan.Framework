# syntax=docker/dockerfile:1
# 由 xihan pack 从 xihan.app.json 生成；重新生成会覆盖本文件。
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
# 就绪探针：chiseled 运行镜像里没有 shell、curl 与 wget，Compose 的 healthcheck 用它探测 /health/ready
COPY <<"EOF" /xihan/healthprobe-src/XiHanHealthProbe.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>XiHanHealthProbe</AssemblyName>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
EOF
COPY <<"EOF" /xihan/healthprobe-src/Program.cs
// 由 xihan pack 生成的就绪探针：向 127.0.0.1:<端口><路径> 发一次 HTTP/1.1 GET，2xx 退出 0，否则退出 1，参数不合法退出 2。
using System.Net.Sockets;
using System.Text;

if (args.Length != 2 || !int.TryParse(args[0], out var port) || port is < 1 or > 65535 || !args[1].StartsWith('/'))
{
    Console.Error.WriteLine("usage: XiHanHealthProbe <port> <path>");
    return 2;
}

var crlf = new string([(char)13, (char)10]);
try
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    using var client = new TcpClient();
    await client.ConnectAsync("127.0.0.1", port, timeout.Token);
    await using var stream = client.GetStream();
    var request = Encoding.ASCII.GetBytes("GET " + args[1] + " HTTP/1.1" + crlf + "Host: localhost" + crlf + "Connection: close" + crlf + crlf);
    await stream.WriteAsync(request, timeout.Token);

    // 状态行形如 HTTP/1.1 200：读满前 12 个字节，第 10 个是状态码的首位
    var status = new byte[12];
    var read = 0;
    while (read < status.Length)
    {
        var count = await stream.ReadAsync(status.AsMemory(read), timeout.Token);
        if (count == 0)
        {
            break;
        }

        read += count;
    }

    return read == status.Length && status[9] == (byte)'2' ? 0 : 1;
}
catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
EOF
RUN dotnet publish /xihan/healthprobe-src/XiHanHealthProbe.csproj --configuration Release --output /xihan/healthprobe-src/publish
WORKDIR /src
COPY . .
RUN dotnet publish "samples/KubernetesApp/KubernetesApp.csproj" --configuration Release --arch $TARGETARCH --output /app/publish --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /xihan/healthprobe-src/publish /xihan/healthprobe
ENV Kestrel__Endpoints__Http__Url=http://0.0.0.0:8080
ENV Kestrel__Endpoints__Grpc__Url=http://0.0.0.0:8081 Kestrel__Endpoints__Grpc__Protocols=Http2
ENV XiHan__Kernel__StopTimeoutSeconds=25
EXPOSE 8080 8081
USER $APP_UID
ENTRYPOINT ["dotnet", "KubernetesApp.dll"]
