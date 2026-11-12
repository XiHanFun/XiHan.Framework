// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using XiHan.Framework.Scheduling.Abstractions;
using XiHan.Framework.Scheduling.Kubernetes.Leases;

namespace XiHan.Framework.Scheduling.Kubernetes.Tests;

/// <summary>
/// 租约客户端照常校验 API Server 证书的主机名与用途（ADR-0201）
/// </summary>
/// <remarks>
/// 安全审查 S-17：原先自定义的证书回调只核对证书链，持有任意一张集群 CA 签发的证书（别的服务的、客户端证书）就能冒充 API Server
/// </remarks>
public sealed class KubernetesLeaseTlsTests : IDisposable
{
    /// <summary>
    /// 测试 CA
    /// </summary>
    private readonly X509Certificate2 _authority;

    /// <summary>
    /// CA 证书文件
    /// </summary>
    private readonly string _authorityPath = Path.Combine(Path.GetTempPath(), $"xihan-k8s-ca-{Guid.NewGuid():N}.crt");

    /// <summary>
    /// 生成测试 CA 并写成 PEM 文件
    /// </summary>
    public KubernetesLeaseTlsTests()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=xihan-test-ca", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        _authority = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllText(_authorityPath, _authority.ExportCertificatePem());
    }

    [Fact]
    public async Task ServerCertificateForTheApiServerAddress_IsAccepted()
    {
        await using var server = await TlsServer.StartAsync(Issue(IPAddress.Loopback, null, serverAuth: true));

        var read = await ClientFor(server).GetAsync("leader", CancellationToken.None);

        Assert.True(read.IsSuccess, read.Error?.ToString());
        Assert.False(read.ValueOrThrow().HasValue);
    }

    [Fact]
    public async Task CertificateForAnotherHost_IsRejected()
    {
        // 修之前链对得上 CA 就放行：集群里别的服务的证书能冒充 API Server
        await using var server = await TlsServer.StartAsync(Issue(null, "other.svc.cluster.local", serverAuth: true));

        var read = await ClientFor(server).GetAsync("leader", CancellationToken.None);

        Assert.Equal(SchedulingErrors.LeaseUnavailable, read.Error?.Code);
        Assert.Equal(0, server.Responses);
    }

    [Fact]
    public async Task CertificateWithoutServerAuthentication_IsRejected()
    {
        // 只能用于客户端认证的证书（如节点的 kubelet 客户端证书）不能当服务端证书
        await using var server = await TlsServer.StartAsync(Issue(IPAddress.Loopback, null, serverAuth: false));

        var read = await ClientFor(server).GetAsync("leader", CancellationToken.None);

        Assert.Equal(SchedulingErrors.LeaseUnavailable, read.Error?.Code);
        Assert.Equal(0, server.Responses);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _authority.Dispose();
        File.Delete(_authorityPath);
    }

    /// <summary>
    /// 指向测试服务端、只信任测试 CA 的 Pod 内客户端
    /// </summary>
    private KubernetesLeaseClient ClientFor(TlsServer server) => KubernetesLeaseClient.CreateInCluster(new KubernetesSchedulingOptions
    {
        ApiServer = new Uri($"https://127.0.0.1:{server.Port}"),
        Namespace = "default",
        Token = "test-token",
        CertificateAuthorityPath = _authorityPath,
    });

    /// <summary>
    /// 由测试 CA 签发一张带私钥的叶证书
    /// </summary>
    /// <remarks>
    /// 主体备用名是给定的 IP 或主机名；<paramref name="serverAuth"/> 为假时用途只有客户端认证
    /// </remarks>
    private X509Certificate2 Issue(IPAddress? address, string? host, bool serverAuth)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=xihan-test-leaf", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        if (address is not null)
        {
            names.AddIpAddress(address);
        }

        if (host is not null)
        {
            names.AddDnsName(host);
        }

        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid(serverAuth ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2")], true));
        using var issued = request.Create(_authority, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), RandomNumberGenerator.GetBytes(8));
        using var withKey = issued.CopyWithPrivateKey(key);
        // 经 PFX 往返：Windows 的 SChannel 不接受内存里临时的私钥
        return X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pfx), null);
    }

    /// <summary>
    /// 只回 404 的最小 TLS 服务端
    /// </summary>
    /// <remarks>
    /// 握手成功的连接读完请求头回 404（租约不存在），握手失败的连接直接关闭
    /// </remarks>
    private sealed class TlsServer : IAsyncDisposable
    {
        /// <summary>
        /// 监听器
        /// </summary>
        private readonly TcpListener _listener;

        /// <summary>
        /// 服务端证书
        /// </summary>
        private readonly X509Certificate2 _certificate;

        /// <summary>
        /// 接受连接的循环
        /// </summary>
        private readonly Task _loop;

        /// <summary>
        /// 已回的响应数
        /// </summary>
        private int _responses;

        /// <summary>
        /// 在回环地址的随机端口上监听
        /// </summary>
        private TlsServer(X509Certificate2 certificate)
        {
            _certificate = certificate;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _loop = Task.Run(AcceptAsync);
        }

        /// <summary>
        /// 端口
        /// </summary>
        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        /// <summary>
        /// 握手成功并回了响应的连接数
        /// </summary>
        public int Responses => Volatile.Read(ref _responses);

        /// <summary>
        /// 启动
        /// </summary>
        public static Task<TlsServer> StartAsync(X509Certificate2 certificate) => Task.FromResult(new TlsServer(certificate));

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            await _loop.ConfigureAwait(false);
            _certificate.Dispose();
        }

        /// <summary>
        /// 逐个接受连接
        /// </summary>
        private async Task AcceptAsync()
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
                {
                    return;
                }

                using (client)
                {
                    await ServeAsync(client).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 握手并回 404
        /// </summary>
        private async Task ServeAsync(TcpClient client)
        {
            try
            {
                await using var tls = new SslStream(client.GetStream());
                await tls.AuthenticateAsServerAsync(_certificate).ConfigureAwait(false);
                var buffer = new byte[4096];
                var received = new StringBuilder();
                while (!received.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    var read = await tls.ReadAsync(buffer).ConfigureAwait(false);
                    if (read == 0)
                    {
                        return;
                    }

                    received.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }

                await tls.WriteAsync("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray()).ConfigureAwait(false);
                Interlocked.Increment(ref _responses);
            }
            catch (Exception exception) when (exception is IOException or System.Security.Authentication.AuthenticationException)
            {
                // 客户端拒绝了证书：握手中断，连接关闭
            }
        }
    }
}
