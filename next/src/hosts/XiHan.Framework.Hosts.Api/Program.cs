namespace XiHan.Framework.Hosts.Api;

/// <summary>
/// 表示 API 宿主程序入口。
/// </summary>
public static class Program
{
    /// <summary>
    /// 程序主入口。
    /// </summary>
    /// <param name="args">启动参数。</param>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddOpenApi();

        var app = builder.Build();

        app.MapGet("/", () => "XiHan.Framework.Next API Host");
        app.MapOpenApi();

        app.Run();
    }
}
