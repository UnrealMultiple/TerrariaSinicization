$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $ScriptDir


# 配置
$BMFontExe = ".\bmfont64.com"
$ConfigFile = ".\Death_Text.bmfc"
$OutputDir = ".\Death_Text"
$FontFile = "$OutputDir\Death_Text.fnt"
$TxtFile = "$OutputDir\Death_Text.txt"
$RebuilderDll = ".\XnaFontRebuilder\bin\Release\net8.0\XnaFontRebuilder.dll"

# 检查必要文件
Write-Host "`n[1/5] 检查必要文件..."
$requiredFiles = @($BMFontExe, $ConfigFile, $RebuilderDll, ".\font.otf")
foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        Write-Error "错误: 找不到文件 $file"
        exit 1
    }
    Write-Host "  ✓ $file"
}

# 确保输出目录存在
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
    Write-Host "  ✓ 创建输出目录: $OutputDir"
}

# 生成BMFont（使用 Start-Process 增强稳定性）
Write-Host "`n[2/5] 使用BMFont生成字体文件..."
try {
    # 转换相对路径为绝对路径，确保 BMFont 能正确找到文件
    $configAbs = Resolve-Path $ConfigFile
    $fontAbs = Join-Path $PWD $FontFile

    $process = Start-Process -FilePath $BMFontExe `
        -ArgumentList "-c `"$configAbs`" -o `"$fontAbs`"" `
        -Wait -PassThru -NoNewWindow -WorkingDirectory $ScriptDir

    if ($process.ExitCode -ne 0) {
        Write-Error "BMFont 生成失败，退出代码: $($process.ExitCode)"
        exit 1
    }
    Write-Host "  ✓ BMFont生成成功"
}
catch {
    Write-Error "启动 BMFont 时发生错误: $_"
    exit 1
}

# 检查生成的文件
if (-not (Test-Path $FontFile)) {
    Write-Error "错误: 未找到生成的.fnt文件"
    exit 1
}

# 统计生成的图片数量
$pngFiles = Get-ChildItem -Path $OutputDir -Filter "Item_Stack_*.png" | Sort-Object Name
Write-Host "  ✓ 生成了 $($pngFiles.Count) 张纹理图片"

# 构建XnaFontRebuilder (如果需要)
Write-Host "`n[3/5] 构建XnaFontRebuilder..."
try {
    Push-Location ".\XnaFontRebuilder"
    dotnet build -c Release --no-incremental | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "XnaFontRebuilder构建失败"
        exit 1
    }
    Pop-Location
    Write-Host "  ✓ XnaFontRebuilder构建成功"
} catch {
    Write-Error "构建错误: $_"
    exit 1
}

# 转换格式
Write-Host "`n[4/5] 转换.fnt为.txt格式..."
try {
    dotnet $RebuilderDll $FontFile $TxtFile --char-spacing 2
    if ($LASTEXITCODE -ne 0) {
        Write-Error "格式转换失败"
        exit 1
    }
    Write-Host "  ✓ 格式转换成功"
} catch {
    Write-Error "转换错误: $_"
    exit 1
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "所有任务完成！" -ForegroundColor Green
Write-Host "输出目录: $((Resolve-Path $OutputDir).Path)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Pop-Location
