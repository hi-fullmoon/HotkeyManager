# HotkeyManager

Windows 全局热键工具：按一个快捷键切换任意应用的显示/隐藏。例如 `Ctrl+Alt+1` 呼出微信，再按一次最小化回去。

## 功能

- 任意数量的「热键 → 应用」映射，全部写在 exe 同目录的 `config.json` 里
- 应用未运行时自动按配置路径启动
- 窗口最小化 → 还原并置前；显示中 → 最小化
- 配置文件保存即热重载，无需重启
- 系统托盘运行，右键菜单可打开配置 / 重载 / 暂停热键 / 开关开机自启 / 退出

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

产物为 `installer/HotkeyManagerSetup-<版本>.exe`：向导可自定义安装路径、可选开机自启/桌面快捷方式；配置保存在安装目录的 `config.json`，安装/卸载均不触碰。

也可以用 `tools/build.ps1` 一键完成「改版本号 → 发布 → 打包」：

```powershell
./tools/build.ps1                 # 沿用当前版本号直接打包
./tools/build.ps1 -Version 0.2.0  # 版本号写入 csproj 和 setup.iss 后再打包
```

只改版本号不打包时用 `tools/bump-version.ps1`：

```powershell
./tools/bump-version.ps1 patch   # 0.1.0 -> 0.1.1（也支持 minor / major）
./tools/bump-version.ps1 1.2.3   # 直接指定版本号
```

## 配置说明（config.json）

配置文件位于 exe 所在目录（安装目录）的 `config.json`，首次运行时自动写入默认模板（内容与仓库根目录 `config.json` 一致），保存即热重载：

```json
{
  "hotkeys": [
    { "key": "alt+1", "processName": "WeChat", "exePath": "C:\\Program Files\\Tencent\\WeChat\\WeChat.exe" }
  ]
}
```

| 字段 | 说明 |
|------|------|
| `key` | 组合键字符串，最后一段为按键，其余为修饰键（`Ctrl` / `Alt` / `Shift` / `Win`），如 `alt+1`、`ctrl+alt+a`。按键参考 WinForms `Keys` 枚举：`1`~`9`、`F1`~`F12`、`A`~`Z`、`NumPad0` 等 |
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
│   └── WindowService.cs    # 窗口查找、显示/隐藏切换、置前
├── Config/
│   ├── AppConfig.cs        # 配置模型
│   └── ConfigManager.cs    # config.json 加载与热重载
├── Tray/
│   └── TrayIcon.cs         # 托盘图标与右键菜单
└── Interop/
    └── User32.cs           # P/Invoke 声明
```
