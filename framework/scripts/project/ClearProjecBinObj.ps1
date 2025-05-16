# 确认是否以管理员权限运行
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Warning "脚本未以管理员权限运行，可能无法删除某些文件。建议以管理员权限运行。"
    Write-Warning "按任意键继续..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

# 获取当前脚本所在目录
$ScriptDirectory = $PSScriptRoot
Write-Host "脚本目录: $ScriptDirectory" -ForegroundColor Cyan

# 构建相对路径的目标目录
$RootPath = Join-Path -Path $ScriptDirectory -ChildPath "..\..\src"
Write-Host "相对路径: $RootPath" -ForegroundColor Cyan

# 解析相对路径为绝对路径
try {
    $RootPath = Resolve-Path -Path $RootPath -ErrorAction Stop | Select-Object -ExpandProperty Path
    Write-Host "目标目录绝对路径: $RootPath" -ForegroundColor Green
}
catch {
    Write-Host "无法解析路径: $($_.Exception.Message)" -ForegroundColor Red
    exit
}

# 检查目标目录是否存在
if (-Not (Test-Path -Path $RootPath)) {
    Write-Host "路径不存在: $RootPath" -ForegroundColor Red
    exit
}

Write-Host "开始清理目录: $RootPath" -ForegroundColor Yellow
$deletedCount = 0
$failedCount = 0

# 查找所有 bin 和 obj 文件夹
$foldersToDelete = Get-ChildItem -Path $RootPath -Recurse -Directory -Force |
Where-Object { $_.Name -in "bin", "obj", "public" }

Write-Host "找到 $($foldersToDelete.Count) 个要删除的文件夹" -ForegroundColor Yellow

# 检查是否有 Visual Studio 或其他可能锁定文件的进程在运行
$vsProcesses = Get-Process | Where-Object { $_.ProcessName -like "*devenv*" -or $_.ProcessName -like "*MSBuild*" }
if ($vsProcesses) {
    Write-Host "发现可能锁定文件的进程正在运行:" -ForegroundColor Red
    $vsProcesses | ForEach-Object { Write-Host " - $($_.ProcessName) (PID: $($_.Id))" -ForegroundColor Red }
    Write-Host "建议关闭这些进程后再运行清理" -ForegroundColor Red
    Write-Host "按任意键继续尝试清理..." -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

# 删除每个文件夹
foreach ($folder in $foldersToDelete) {
    Write-Host "尝试删除: $($folder.FullName)" -ForegroundColor Cyan
    try {
        # 强制删除只读文件和系统文件
        Get-ChildItem -Path $folder.FullName -Recurse -Force -ErrorAction SilentlyContinue | 
        ForEach-Object { 
            Set-ItemProperty -Path $_.FullName -Name IsReadOnly -Value $false -ErrorAction SilentlyContinue 
        }
        
        # 尝试删除文件夹
        Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction Stop
        
        # 延迟确保释放资源
        Start-Sleep -Milliseconds 100
        
        # 验证文件夹是否被成功删除
        if (Test-Path -Path $folder.FullName) {
            Write-Host "删除失败: $($folder.FullName) - 文件夹仍然存在" -ForegroundColor Red
            $failedCount++
        }
        else {
            Write-Host "已成功删除: $($folder.FullName)" -ForegroundColor Green
            $deletedCount++
        }
    }
    catch {
        Write-Host "删除失败: $($folder.FullName)" -ForegroundColor Red
        Write-Host "错误: $($_.Exception.Message)" -ForegroundColor Red
        
        # 尝试识别哪些文件被锁定
        try {
            $lockedFiles = Get-ChildItem -Path $folder.FullName -Recurse -Force -ErrorAction SilentlyContinue |
            ForEach-Object {
                try {
                    $fileStream = [System.IO.File]::Open($_.FullName, 'Open', 'Write')
                    $fileStream.Close()
                    $fileStream.Dispose()
                    return $null
                }
                catch {
                    return $_.FullName
                }
            } | Where-Object { $_ -ne $null }
            
            if ($lockedFiles) {
                Write-Host "以下文件可能被锁定:" -ForegroundColor Red
                $lockedFiles | Select-Object -First 5 | ForEach-Object {
                    Write-Host " - $_" -ForegroundColor Red
                }
                if ($lockedFiles.Count -gt 5) {
                    Write-Host "... 以及更多 $($lockedFiles.Count - 5) 个文件" -ForegroundColor Red
                }
            }
        }
        catch {
            Write-Host "无法识别被锁定的文件: $($_.Exception.Message)" -ForegroundColor Red
        }
        
        $failedCount++
    }
}

Write-Host "清理完成！" -ForegroundColor Green
Write-Host "成功删除: $deletedCount 个文件夹" -ForegroundColor Green
Write-Host "删除失败: $failedCount 个文件夹" -ForegroundColor $(if ($failedCount -gt 0) { "Red" } else { "Green" })

if ($failedCount -gt 0) {
    Write-Host "建议:" -ForegroundColor Yellow
    Write-Host "1. 确保以管理员权限运行此脚本" -ForegroundColor Yellow
    Write-Host "2. 关闭所有可能锁定文件的程序（如 Visual Studio、编译进程等）" -ForegroundColor Yellow
    Write-Host "3. 重新运行脚本" -ForegroundColor Yellow
}

# 等待用户确认
Write-Host "按任意键退出..." -ForegroundColor Cyan
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
