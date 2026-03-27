# FontBuilder.ps1
# 统一的字体生成脚本 - 支持批量生成和单独生成

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $ScriptDir

# 字体配置列表
$fontConfigs = @{
    "Item_Stack" = @{
        ConfigFile = ".\Item_Stack.bmfc"
        OutputDir = ".\Item_Stack"
        FontFile = "Item_Stack.fnt"
        TxtFile = "Item_Stack.txt"
        Description = "物品堆叠数字字体"
        CharInfoFile = ".\FontInfo\Item_Stack.txt"
    }
    "Combat_Crit" = @{
        ConfigFile = ".\Combat_Crit.bmfc"
        OutputDir = ".\Combat_Crit"
        FontFile = "Combat_Crit.fnt"
        TxtFile = "Combat_Crit.txt"
        Description = "战斗暴击数字字体"
        CharInfoFile = ".\FontInfo\Combat_Crit.txt"
    }
    "Combat_Text" = @{
        ConfigFile = ".\Combat_Text.bmfc"
        OutputDir = ".\Combat_Text"
        FontFile = "Combat_Text.fnt"
        TxtFile = "Combat_Text.txt"
        Description = "战斗文字字体"
        CharInfoFile = ".\FontInfo\Combat_Text.txt"
    }
    "Death_Text" = @{
        ConfigFile = ".\Death_Text.bmfc"
        OutputDir = ".\Death_Text"
        FontFile = "Death_Text.fnt"
        TxtFile = "Death_Text.txt"
        Description = "死亡文字字体"
        CharInfoFile = ".\FontInfo\Death_Text.txt"
    }
    "Mouse_Text" = @{
        ConfigFile = ".\Mouse_Text.bmfc"
        OutputDir = ".\Mouse_Text"
        FontFile = "Mouse_Text.fnt"
        TxtFile = "Mouse_Text.txt"
        Description = "鼠标提示文字字体"
        CharInfoFile = ".\FontInfo\Mouse_Text.txt"
    }
}

# 全局配置
$BMFontExe = ".\bmfont64.com"
$XnaFontRebuilder = ".\XnaFontRebuilder\bin\Release\net8.0\XnaFontRebuilder.dll"
$SourceFont = ".\font.otf"
$FontInfoDir = ".\FontInfo"

# 公共函数：检查环境
function Test-Environment {
    Write-Host "`n[环境检查]" -ForegroundColor Cyan
    
    # 检查 .NET SDK
    $dotnetVersion = dotnet --version 2>$null
    if (-not $dotnetVersion) {
        Write-Host "  ✗ 未检测到 .NET SDK" -ForegroundColor Red
        Write-Host "    请安装 .NET 8.0 SDK: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
        return $false
    }
    Write-Host "  ✓ .NET SDK $dotnetVersion" -ForegroundColor Green
    
    # 检查 BMFont
    if (-not (Test-Path $BMFontExe)) {
        Write-Host "  ✗ 未找到 bmfont64.com" -ForegroundColor Red
        return $false
    }
    Write-Host "  ✓ bmfont64.com" -ForegroundColor Green
    
    # 检查源字体
    if (-not (Test-Path $SourceFont)) {
        Write-Host "  ✗ 未找到 font.otf" -ForegroundColor Red
        return $false
    }
    Write-Host "  ✓ font.otf" -ForegroundColor Green
    
    # 检查 FontInfo 目录
    if (-not (Test-Path $FontInfoDir)) {
        Write-Host "  ✗ 未找到 FontInfo 目录" -ForegroundColor Red
        return $false
    }
    Write-Host "  ✓ FontInfo 目录" -ForegroundColor Green
    
    # 检查 XnaFontRebuilder 项目
    if (-not (Test-Path ".\XnaFontRebuilder\XnaFontRebuilder.csproj")) {
        Write-Host "  ✗ 未找到 XnaFontRebuilder 项目" -ForegroundColor Red
        return $false
    }
    Write-Host "  ✓ XnaFontRebuilder 项目" -ForegroundColor Green
    
    return $true
}

# 公共函数：构建 XnaFontRebuilder
function Build-XnaFontRebuilder {
    Write-Host "`n[构建 XnaFontRebuilder]" -ForegroundColor Cyan
    
    if (-not (Test-Path $XnaFontRebuilder)) {
        Write-Host "  正在构建..." -ForegroundColor Yellow
        try {
            Push-Location ".\XnaFontRebuilder"
            dotnet build -c Release --no-incremental | Out-Null
            if ($LASTEXITCODE -ne 0) {
                throw "构建失败"
            }
            Pop-Location
            Write-Host "  ✓ 构建成功" -ForegroundColor Green
        }
        catch {
            Write-Host "  ✗ 构建失败: $_" -ForegroundColor Red
            return $false
        }
    }
    else {
        Write-Host "  ✓ 已存在，跳过构建" -ForegroundColor Green
    }
    
    return $true
}

# 公共函数：生成配置文件
function Generate-ConfigFile {
    param(
        [string]$FontName,
        [hashtable]$Config
    )
    
    Write-Host "  [0/3] 生成配置文件..." -ForegroundColor Yellow
    
    # 检查字符信息文件是否存在
    if (-not (Test-Path $Config.CharInfoFile)) {
        Write-Host "    ✗ 字符信息文件不存在: $($Config.CharInfoFile)" -ForegroundColor Red
        return $false
    }
    
    # 检查是否存在对应的 txt 字体文件（用于读取字体大小）
    $existingTxtFile = Join-Path $Config.OutputDir $Config.TxtFile
    $fromFontParam = if (Test-Path $existingTxtFile) { "--from-font `"$existingTxtFile`"" } else { "" }
    
    try {
        # 使用 --build-from-chars 命令生成配置文件
        # 从字符信息文件生成，自动检测字体大小（如果存在现有字体文件）
        $cmd = "dotnet `"$XnaFontRebuilder`" --build-cfg-auto `"$($Config.CharInfoFile)`" `"$($Config.ConfigFile)`""
        
        if ($fromFontParam) {
            $cmd += " $fromFontParam"
            Write-Host "    使用现有字体文件检测大小: $existingTxtFile" -ForegroundColor Gray
        } else {
            Write-Host "    使用默认字体大小: 62" -ForegroundColor Gray
        }
        
        Invoke-Expression $cmd
        
        if ($LASTEXITCODE -ne 0) {
            throw "配置文件生成失败，退出代码: $LASTEXITCODE"
        }
        
        if (-not (Test-Path $Config.ConfigFile)) {
            throw "未找到生成的配置文件"
        }
        
        Write-Host "    ✓ 配置文件生成成功: $($Config.ConfigFile)" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "    ✗ 失败: $_" -ForegroundColor Red
        return $false
    }
}

# 公共函数：生成单个字体
function Generate-Font {
    param(
        [string]$FontName,
        [hashtable]$Config
    )
    
    Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
    Write-Host "  生成字体: $FontName" -ForegroundColor Cyan
    Write-Host "  描述: $($Config.Description)" -ForegroundColor Gray
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Magenta
    
    $startTime = Get-Date
    
    # 步骤0: 生成配置文件
    if (-not (Generate-ConfigFile -FontName $FontName -Config $Config)) {
        return $false
    }
    
    # 确保输出目录存在
    if (-not (Test-Path $Config.OutputDir)) {
        New-Item -ItemType Directory -Path $Config.OutputDir -Force | Out-Null
        Write-Host "  ✓ 创建输出目录: $($Config.OutputDir)" -ForegroundColor Gray
    }
    
    $fontPath = Join-Path $Config.OutputDir $Config.FontFile
    $txtPath = Join-Path $Config.OutputDir $Config.TxtFile
    
    # 步骤1: 生成 BMFont
    Write-Host "  [1/3] 生成 BMFont 文件..." -ForegroundColor Yellow
    try {
        $configAbs = Resolve-Path $Config.ConfigFile
        $fontAbs = Join-Path $PWD $fontPath
        
        $process = Start-Process -FilePath $BMFontExe `
            -ArgumentList "-c `"$configAbs`" -o `"$fontAbs`"" `
            -Wait -PassThru -NoNewWindow -WorkingDirectory $ScriptDir
        
        if ($process.ExitCode -ne 0) {
            throw "BMFont 生成失败，退出代码: $($process.ExitCode)"
        }
        
        if (-not (Test-Path $fontPath)) {
            throw "未找到生成的 .fnt 文件"
        }
        
        # 统计生成的图片
        $pngFiles = Get-ChildItem -Path $Config.OutputDir -Filter "$($FontName)_*.png" -ErrorAction SilentlyContinue
        Write-Host "    ✓ 生成成功，纹理图片: $($pngFiles.Count) 张" -ForegroundColor Green
    }
    catch {
        Write-Host "    ✗ 失败: $_" -ForegroundColor Red
        return $false
    }
    
    # 步骤2: 转换格式（使用新的 --convert 命令）
    Write-Host "  [2/3] 转换为 TXT 格式..." -ForegroundColor Yellow
    try {
        # 使用新的命令格式：--convert <input.fnt> <output.txt> [options]
        dotnet $XnaFontRebuilder --convert $fontPath $txtPath --latin-compensation 0.5 --char-spacing 1
        
        if ($LASTEXITCODE -ne 0) {
            throw "格式转换失败，退出代码: $LASTEXITCODE"
        }
        
        if (-not (Test-Path $txtPath)) {
            throw "未找到生成的 .txt 文件"
        }
        
        Write-Host "    ✓ 转换成功" -ForegroundColor Green
    }
    catch {
        Write-Host "    ✗ 失败: $_" -ForegroundColor Red
        return $false
    }
    
    # 步骤3: 验证输出
    Write-Host "  [3/3] 验证输出文件..." -ForegroundColor Yellow
    
    $fntSize = (Get-Item $fontPath).Length
    $txtSize = (Get-Item $txtPath).Length
    $pngCount = (Get-ChildItem -Path $Config.OutputDir -Filter "*.png").Count
    
    Write-Host "    ✓ .fnt: $([math]::Round($fntSize/1KB, 2)) KB" -ForegroundColor Green
    Write-Host "    ✓ .txt: $([math]::Round($txtSize/1KB, 2)) KB" -ForegroundColor Green
    Write-Host "    ✓ 纹理: $pngCount 张图片" -ForegroundColor Green
    
    # 删除临时配置文件
    if (Test-Path $Config.ConfigFile) {
        Remove-Item $Config.ConfigFile -Force
        Write-Host "    ✓ 删除临时配置文件: $($Config.ConfigFile)"  -ForegroundColor Green
    }
    
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalSeconds
    Write-Host "  ✅ $FontName 生成完成，耗时: $([math]::Round($duration, 2)) 秒" -ForegroundColor Green
    
    return $true
}

# 公共函数：列出所有可用字体
function Show-AvailableFonts {
    Write-Host "`n可用字体列表:" -ForegroundColor Cyan
    Write-Host ("{0,-15} {1,-30} {2,-20} {3}" -f "名称", "描述", "字符信息文件", "配置文件") -ForegroundColor Gray
    Write-Host ("{0,-15} {1,-30} {2,-20} {3}" -f "----", "----", "--------", "--------") -ForegroundColor Gray
    
    foreach ($name in $fontConfigs.Keys | Sort-Object) {
        $config = $fontConfigs[$name]
        $charExists = if (Test-Path $config.CharInfoFile) { "✓" } else { "✗" }
        $cfgExists = if (Test-Path $config.ConfigFile) { "✓" } else { "✗" }
        Write-Host ("{0,-15} {1,-30} {2,-20} {3}" -f $name, $config.Description, $charExists, $cfgExists)
    }
}

# 显示帮助信息
function Show-Help {
    Write-Host @"
╔══════════════════════════════════════════════════════════════╗
║                    字体生成工具 v3.0                         ║
╠══════════════════════════════════════════════════════════════╣
║ 功能: 从 FontInfo 目录的字符信息文件生成字体                 ║
║       自动生成配置文件 -> 调用 BMFont -> 转换为 XNA 格式     ║
╠══════════════════════════════════════════════════════════════╣
║ 用法:                                                        ║
║   .\FontBuilder.ps1 [参数]                                   ║
╠══════════════════════════════════════════════════════════════╣
║ 参数:                                                        ║
║   无参数           - 生成所有字体                            ║
║   -List           - 列出所有可用字体                         ║
║   -Help           - 显示此帮助信息                           ║
║   -Font <名称>    - 生成指定字体                             ║
║   -Rebuild        - 强制重新构建 XnaFontRebuilder            ║
╠══════════════════════════════════════════════════════════════╣
║ 示例:                                                        ║
║   .\FontBuilder.ps1                    # 生成所有字体        ║
║   .\FontBuilder.ps1 -List              # 列出所有字体        ║
║   .\FontBuilder.ps1 -Font Item_Stack   # 生成单个字体        ║
║   .\FontBuilder.ps1 -Rebuild           # 重新构建并生成所有  ║
╚══════════════════════════════════════════════════════════════╝
"@
}

# 主函数
function Main {
    param(
        [switch]$List,
        [switch]$Help,
        [string]$Font,
        [switch]$Rebuild
    )
    
    # 显示帮助
    if ($Help) {
        Show-Help
        return
    }
    
    # 列出字体
    if ($List) {
        Show-AvailableFonts
        return
    }
    
    # 显示标题
    Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                    字体批量生成工具 v3.0                      ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host "开始时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Yellow
    
    # 环境检查
    if (-not (Test-Environment)) {
        Write-Host "`n❌ 环境检查失败，请解决上述问题后重试" -ForegroundColor Red
        exit 1
    }
    
    # 构建 XnaFontRebuilder
    if ($Rebuild) {
        Write-Host "`n[强制重新构建]" -ForegroundColor Yellow
        if (Test-Path $XnaFontRebuilder) {
            Remove-Item $XnaFontRebuilder -Force
        }
    }
    
    if (-not (Build-XnaFontRebuilder)) {
        Write-Host "`n❌ XnaFontRebuilder 构建失败" -ForegroundColor Red
        exit 1
    }
    
    # 确定要生成的字体列表
    $fontsToGenerate = @{}
    
    if ($Font) {
        # 生成单个字体
        if ($fontConfigs.ContainsKey($Font)) {
            $fontsToGenerate[$Font] = $fontConfigs[$Font]
            Write-Host "`n🎯 目标字体: $Font" -ForegroundColor Cyan
        } else {
            Write-Host "`n❌ 未知字体: $Font" -ForegroundColor Red
            Write-Host "可用字体: $($fontConfigs.Keys -join ', ')" -ForegroundColor Yellow
            exit 1
        }
    } else {
        # 生成所有字体
        $fontsToGenerate = $fontConfigs
        Write-Host "`n🎯 目标: 生成所有字体 ($($fontConfigs.Count) 个)" -ForegroundColor Cyan
    }
    
    # 执行生成
    $successList = @()
    $failList = @()
    $totalStart = Get-Date
    
    foreach ($name in $fontsToGenerate.Keys | Sort-Object) {
        $result = Generate-Font -FontName $name -Config $fontsToGenerate[$name]
        if ($result) {
            $successList += $name
        } else {
            $failList += $name
        }
    }
    
    # 输出总结
    $totalEnd = Get-Date
    $totalDuration = ($totalEnd - $totalStart).TotalSeconds
    
    Write-Host "`n╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                        执行结果汇总                           ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host "完成时间: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Yellow
    Write-Host "总耗时: $([math]::Round($totalDuration, 2)) 秒" -ForegroundColor Yellow
    Write-Host ""
    
    Write-Host "✅ 成功: $($successList.Count) 个" -ForegroundColor Green
    if ($successList.Count -gt 0) {
        foreach ($name in $successList) {
            $outputDir = $fontConfigs[$name].OutputDir
            Write-Host "   • $name -> $outputDir" -ForegroundColor Gray
        }
    }
    
    if ($failList.Count -gt 0) {
        Write-Host "`n❌ 失败: $($failList.Count) 个" -ForegroundColor Red
        foreach ($name in $failList) {
            Write-Host "   • $name" -ForegroundColor Red
        }
    }
    
    Write-Host ""
    
    if ($failList.Count -eq 0) {
        Write-Host "🎉 所有字体生成成功！" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "⚠️  部分字体生成失败，请检查上述错误信息" -ForegroundColor Yellow
        exit 1
    }
}

# 解析参数并执行
Main @args

Pop-Location