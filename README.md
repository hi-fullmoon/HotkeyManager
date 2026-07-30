# HotkeyManager

Windows 全局热键工具：按一个快捷键切换任意应用的显示/隐藏。例如 `Ctrl+Alt+1` 呼出微信，再按一次最小化回去。

## 功能

- 任意数量的「热键 → 应用」映射，全部写在 `config.json` 里
- 应用未运行时自动按配置路径启动
- 窗口最小化/隐藏 → 还原并置前；显示中 → 最小化（或彻底隐藏，可配）
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

产物在 `bin/Release/net8.0-windows/win-x64/publish/HotkeyManager.exe`，连同旁边的 `config.json` 一起拷走即可。

制作安装包（需安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)，脚本会引用发布产物）：

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer/setup.iss
```

产物为 `installer/HotkeyManagerSetup-<版本>.exe`：向导可自定义安装路径、可选开机自启/桌面快捷方式，卸载时保留 `config.json`。

## 配置说明（config.json）

```json
{
  "Hotkeys": [
    {
      "Modifiers": "Ctrl+Alt",
      "Key": "D1",
      "HideMode": "minimize",
      "Target": {
        "DisplayName": "微信",
        "ProcessName": "WeChat",
        "ExePath": "C:\\Program Files\\Tencent\\WeChat\\WeChat.exe",
        "WindowClass": "WeChatMainWndForPC"
      }
    }
  ]
}
```

| 字段 | 说明 |
|------|------|
| `Modifiers` | 修饰键，`+` 分隔：`Ctrl` / `Alt` / `Shift` / `Win` |
| `Key` | 按键名，对应 WinForms `Keys` 枚举：`D1`~`D9`、`F1`~`F12`、`A`~`Z`、`NumPad0` 等 |
| `HideMode` | `minimize`（最小化，默认）或 `hide`（彻底隐藏窗口） |
| `Target.ProcessName` | 进程名，不含 `.exe` |
| `Target.ExePath` | 进程未运行时用于启动的路径 |
| `Target.WindowClass` | 可选。窗口类名（用 Spy++ / Window Detective 查看），填写后优先按类名查找，能定位到最小化到托盘的隐藏窗口 |

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
│   ├── HotkeyParser.cs     # "Ctrl+Alt" / "D1" → 修饰键标志 + 虚拟键码
│   └── WindowService.cs    # 窗口查找、显示/隐藏切换、置前
├── Config/
│   ├── AppConfig.cs        # 配置模型
│   └── ConfigManager.cs    # config.json 加载与热重载
├── Tray/
│   └── TrayIcon.cs         # 托盘图标与右键菜单
└── Interop/
    └── User32.cs           # P/Invoke 声明
```
