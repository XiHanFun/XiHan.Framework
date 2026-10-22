# syntax=docker/dockerfile:1
# 由 xihan pack 从 xihan.app.json 生成；重新生成会覆盖本文件。
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN dotnet publish "samples/KubernetesApp/KubernetesApp.csproj" --configuration Release --arch $TARGETARCH --output /app/publish --self-contained false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV Kestrel__Endpoints__Http__Url=http://0.0.0.0:8080
ENV Kestrel__Endpoints__Grpc__Url=http://0.0.0.0:8081 Kestrel__Endpoints__Grpc__Protocols=Http2
ENV XiHan__Kernel__StopTimeoutSeconds=25
EXPOSE 8080 8081
USER $APP_UID
ENTRYPOINT ["dotnet", "KubernetesApp.dll"]
