# 设置控制台编码为 UTF-8 以正确显示中文
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# 从环境变量读取 API 密钥
$apiKey = [System.Environment]::GetEnvironmentVariable("NUGET_API_KEY", "User")

# 检查 API 密钥是否存在
if (-not $apiKey) {
    Write-Error "未找到 NuGet API 密钥。请先运行 SaveNugetApiKey.ps1 设置 API 密钥。"
    exit 1
}

# NuGet 源地址
$sourceUrl = "https://api.nuget.org/v3/index.json"

# 提示用户输入包名
Write-Output "========================================="
Write-Output "  批量 Unlist NuGet 包版本"
Write-Output "========================================="
Write-Output ""

$packageName = Read-Host "请输入要 Unlist 的包名"
if (-not $packageName) {
    Write-Error "包名不能为空。"
    exit 1
}

Write-Output "正在查询包 '$packageName' 的所有版本..."

try {
    # 使用 NuGet API 查询包的所有版本
    $packageUrl = "https://api.nuget.org/v3-flatcontainer/$($packageName.ToLower())/index.json"
    $response = Invoke-RestMethod -Uri $packageUrl -Method Get
    
    if (-not $response -or -not $response.versions) {
        Write-Error "未找到包 '$packageName' 或无法获取版本信息。请检查包名是否正确。"
        exit 1
    }
    
    $versions = $response.versions
    Write-Output "找到 $($versions.Count) 个版本:"
    
    # 显示所有版本
    for ($i = 0; $i -lt $versions.Count; $i++) {
        Write-Output "  [$i] $($versions[$i])"
    }
    
    Write-Output ""
    Write-Output "选择要 Unlist 的版本:"
    Write-Output "0: 全部版本"
    Write-Output "1: 指定单个版本"
    Write-Output "2: 指定版本范围"
    Write-Output "3: 预发布版本 (alpha, beta, preview, rc)"
    Write-Output "4: 稳定版本"
    
    $choice = Read-Host "请选择 (0-4)"
    
    $versionsToUnlist = @()
    
    switch ($choice) {
        "0" {
            $versionsToUnlist = $versions
            Write-Output "将 Unlist 所有 $($versions.Count) 个版本"
        }
        "1" {
            $versionIndex = Read-Host "请输入版本索引 (0-$($versions.Count-1))"
            if ($versionIndex -match '^\d+$' -and [int]$versionIndex -ge 0 -and [int]$versionIndex -lt $versions.Count) {
                $versionsToUnlist = @($versions[[int]$versionIndex])
                Write-Output "将 Unlist 版本: $($versionsToUnlist[0])"
            }
            else {
                Write-Error "无效的版本索引。"
                exit 1
            }
        }
        "2" {
            $startIndex = Read-Host "请输入起始索引 (0-$($versions.Count-1))"
            $endIndex = Read-Host "请输入结束索引 (0-$($versions.Count-1))"
            
            if ($startIndex -match '^\d+$' -and $endIndex -match '^\d+$' -and 
                [int]$startIndex -ge 0 -and [int]$startIndex -lt $versions.Count -and
                [int]$endIndex -ge 0 -and [int]$endIndex -lt $versions.Count -and
                [int]$startIndex -le [int]$endIndex) {
                
                $versionsToUnlist = $versions[[int]$startIndex..[int]$endIndex]
                Write-Output "将 Unlist $($versionsToUnlist.Count) 个版本 (索引 $startIndex 到 $endIndex)"
            }
            else {
                Write-Error "无效的索引范围。"
                exit 1
            }
        }
        "3" {
            # 预发布版本 (包含 -)
            $versionsToUnlist = $versions | Where-Object { $_ -like "*-*" }
            Write-Output "将 Unlist $($versionsToUnlist.Count) 个预发布版本"
        }
        "4" {
            # 稳定版本 (不包含 -)
            $versionsToUnlist = $versions | Where-Object { $_ -notlike "*-*" }
            Write-Output "将 Unlist $($versionsToUnlist.Count) 个稳定版本"
        }
        default {
            Write-Error "无效的选择。"
            exit 1
        }
    }
    
    if ($versionsToUnlist.Count -eq 0) {
        Write-Output "没有找到符合条件的版本。"
        exit 0
    }
    
    # 显示将要 Unlist 的版本
    Write-Output ""
    Write-Output "将要 Unlist 的版本:"
    foreach ($version in $versionsToUnlist) {
        Write-Output "  - $version"
    }
    
    Write-Output ""
    Write-Warning "警告: Unlist 操作将使这些版本在 NuGet.org 搜索结果中不可见!"
    Write-Warning "但是如果有人知道确切的版本号，仍然可以下载这些包。"
    Write-Output ""
    
    $confirm = Read-Host "是否确认执行 Unlist 操作? (Y/y 或 N/n)"
    if ($confirm -ne "Y" -and $confirm -ne "y") {
        Write-Output "操作已取消。"
        exit 0
    }
    
    # 执行 Unlist 操作
    $successCount = 0
    $failCount = 0
    
    Write-Output ""
    Write-Output "开始执行 Unlist 操作..."
    
    foreach ($version in $versionsToUnlist) {
        Write-Output "正在 Unlist: $packageName $version"
        
        try {
            # 使用 dotnet nuget delete 命令来 unlist 包
            # 注意：这个命令实际上是 unlist 而不是真正删除
            $result = dotnet nuget delete $packageName $version --api-key $apiKey --source $sourceUrl --non-interactive 2>&1
            # 输出命令
            Write-Output "  结果: $result"
            
            if ($LASTEXITCODE -eq 0) {
                Write-Output "  成功: $packageName $version"
                $successCount++
            }
            else {
                Write-Warning "  失败: $packageName $version"
                Write-Warning "    错误信息: $result"
                $failCount++
            }
        }
        catch {
            Write-Warning "  异常: $packageName $version"
            Write-Warning "    异常信息: $($_.Exception.Message)"
            $failCount++
        }
        
        # 添加延迟以避免 API 限制
        Start-Sleep -Milliseconds 500
    }
    
    Write-Output ""
    Write-Output "========================================="
    Write-Output "操作完成!"
    Write-Output "成功: $successCount 个版本"
    Write-Output "失败: $failCount 个版本"
    Write-Output "总计: $($versionsToUnlist.Count) 个版本"
    Write-Output "========================================="
    
    if ($failCount -gt 0) {
        Write-Warning "部分版本 Unlist 失败，请检查网络连接和 API 密钥权限。"
        exit 1
    }
}
catch {
    Write-Error "查询包版本时发生错误: $($_.Exception.Message)"
    Write-Error "请检查包名是否正确以及网络连接是否正常。"
    exit 1
} 