# HotkeyManager

Windows 全局热键工具：按一个快捷键切换任意应用的显示/隐藏。例如 `Ctrl+Alt+1` 呼出微信，再按一次最小化回去。

## 功能

- 任意数量的「热键 → 应用」映射，全部写在 exe 同目录的 `config.jsonc` 里
- 应用未运行时自动按配置路径启动
- 窗口最小化 → 还原并置前；显示中 → 最小化
- 配置文件保存即热重载，无需重启
- 配置支持注释和尾随逗号，默认模板内置字段说明与可复制示例
- 系统托盘运行，右键菜单可打开快捷键设置 / 暂停热键 / 开关开机自启 / 退出；双击托盘图标也可打开设置窗口
- 同一 Windows 用户只运行一个实例，重复启动不会创建第二个托盘图标

## 构建与运行（需在 Windows 上）

```powershell
cd src/HotkeyManager
dotnet run
```

发布单文件 exe（无需安装 .NET 运行时）：

```powershell
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

产物在 `bin/Release/net8.0-windows/win-x64/publish/HotkeyManager.exe`，单个 exe 拷走即可；配置文件首次运行时自动生成。

制作安装包（需安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)，脚本会引用发布产物）：

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer/setup.iss
```

产物为 `installer/HotkeyManagerSetup-<版本>.exe`：向导可自定义安装路径、可选开机自启/桌面快捷方式；配置保存在安装目录的 `config.jsonc`，安装/卸载均不触碰。

也可以用 `tools/build.ps1` 一键完成「改版本号 → 发布 → 打包」：

```powershell
./tools/build.ps1                 # 沿用当前版本号直接打包
./tools/build.ps1 -Version 1.1.0  # 同步版本、归档 Changelog 后再打包
```

构建完整发布包：

```powershell
./tools/package-release.ps1
```

产物位于 `dist/`：

- `HotkeyManager-<版本>-Windows-x64-Setup.exe`：安装包
- `HotkeyManager-<版本>-Windows-x64-portable.zip`：便携版
- 每个产物对应的 `.sha256` 校验文件

## 自动构建与发布

GitHub Actions 会在以下情况运行：

- Pull Request 和 `main` 分支提交：构建安装包与便携版，并保留 14 天的构建产物。
- 推送 `vX.Y.Z` 标签：创建 GitHub Release，上传安装包、便携版和 SHA-256 文件，并使用对应 Changelog 作为发布说明。
- 推送 `vX.Y.Z-suffix` 标签：例如 `v1.1.0-beta.1`，发布为 pre-release。

发布前先在 `[Unreleased]` 记录变更，再更新版本并归档 Changelog：

```powershell
# 先把本次变更写入 CHANGELOG.md 的 [Unreleased]
./tools/bump-version.ps1 minor   # 也支持 major / patch / 直接指定 X.Y.Z

git add src/HotkeyManager/HotkeyManager.csproj installer/setup.iss CHANGELOG.md
git commit -m "chore: bump version to 1.1.0"
git tag v1.1.0
git push origin main v1.1.0
```

`bump-version.ps1` 会同步项目与安装器版本，并将 `[Unreleased]` 归档为带日期的新版本。可使用 `-Date YYYY-MM-DD` 指定日期、`-SkipChangelog` 仅更新版本文件，或用 `-DryRun` 预览。工作流会校验 tag 与项目版本一致，避免误发版本。

## 配置说明（config.jsonc）

配置文件位于 exe 所在目录（安装目录）的 `config.jsonc`，首次运行时自动写入带注释的引导模板（内容与仓库根目录 `config.jsonc` 一致），保存即热重载。右键托盘图标选择「快捷键设置」，或直接双击托盘图标即可打开设置窗口。窗口固定为 760×540 逻辑像素，按显示器 DPI 缩放，禁止最大化及拖动调整大小。列表每页 6 项，使用分页避免滚动条。支持搜索、新建、删除、选择 exe、从运行中的应用选择、录制组合键与保存；录制期间临时释放本程序的热键，结束后按原暂停状态恢复。关闭窗口后继续在托盘运行。

点击应用路径旁的「从运行中选择」，可搜索并选择有可见窗口的应用，自动填入可执行文件路径和真实进程名。自定义名称会保留；新建条目会自动补上应用名称。选择弹框支持搜索、刷新和列表内部滚动，不分页；无法读取路径的项目会禁用并提示手动选择 exe；选择后仍需点击「保存更改」。

设置窗口使用 Microsoft Edge WebView2 Runtime（Windows 11 通常已内置）；缺失时需从微软官方安装运行时。界面资源内嵌在 exe 中，无需网络加载。

保存前会校验配置内重复及系统占用。保存采用原子替换，上一版完整配置（含注释）备份为 `config.jsonc.bak`；新文件写为格式化 JSON，保留现有未知字段与 `windowClass`。如果文件被外部编辑器修改，窗口会拒绝覆盖，点击「重新加载配置」后再编辑。仍可直接编辑配置文件并热重载。

```jsonc
{
  // 可以添加任意数量的配置对象；末尾逗号也是允许的。
  "hotkeys": [
    {
      "key": "ctrl+alt+1",
      "processName": "notepad",
      "exePath": "C:\\Windows\\System32\\notepad.exe",
      "windowClass": "",
    },
  ]
}
```

| 字段 | 说明 |
|------|------|
| `name` | 可选。设置窗口中显示的应用名称；旧配置未填写时显示进程名称 |
| `key` | 组合键字符串，最后一段为按键，其余为修饰键（`Ctrl` / `Alt` / `Shift` / `Win`），如 `alt+1`、`ctrl+alt+a`。普通按键建议搭配至少一个修饰键，`F1`~`F24` 可单独使用；按键还支持 `A`~`Z`、`NumPad0` 等 WinForms `Keys` 名称 |
| `processName` | 进程名，不含 `.exe` |
| `exePath` | 进程未运行时用于启动的路径 |
| `windowClass` | 可选。窗口类名（用 Spy++ / Window Detective 查看），填写后优先按类名查找，能定位到最小化到托盘的隐藏窗口 |

注意：

- 热键被其他程序先注册时会失败，托盘会弹提示，换一个组合即可
- 微信老版本类名是 `WeChatMainWndForPC`，新版（4.0+）会变，建议用 Spy++ 实测后填入
- 要控制以管理员身份运行的应用，本程序也需以管理员身份运行

## 项目结构

```
src/HotkeyManager/
├── Program.cs              # 入口，启动托盘 + 消息循环
├── HotkeyAppContext.cs     # 组装各服务，应用配置
├── Core/
│   ├── HotkeyService.cs    # RegisterHotKey 封装，WM_HOTKEY 分发
│   ├── HotkeyParser.cs     # "ctrl+alt+1" → 修饰键标志 + 虚拟键码
│   ├── SingleInstanceGuard.cs # 按 Windows 用户隔离的单实例保护
│   └── WindowService.cs    # 窗口查找、显示/隐藏切换、置前
├── Config/
│   ├── AppConfig.cs        # 配置模型
│   └── ConfigManager.cs    # config.jsonc 加载与热重载
├── Tray/
│   └── TrayIcon.cs         # 托盘图标与右键菜单
└── Interop/
    └── User32.cs           # P/Invoke 声明
```

## 设置窗口验证

```powershell
dotnet run --project tools/SettingsChecks/SettingsChecks.csproj
node tools/SettingsChecks/ui-checks.cjs
```

原生检查使用临时配置，覆盖 JSONC 读取、备份、字段保留、并发修改、系统占用和重复按键；不会改写实际配置。
