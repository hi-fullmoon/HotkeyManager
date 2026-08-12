; HotkeyManager 安装包脚本
; 构建：先 dotnet publish 出单文件 exe，再运行 ISCC 编译本脚本
;   dotnet publish src/HotkeyManager -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\setup.iss

#define AppName "HotkeyManager"
#define AppVersion "1.0.2"
#define PublishDir "..\src\HotkeyManager\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
; 普通权限即可安装；允许用户在向导里切换"仅当前用户/所有用户"
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=.
OutputBaseFilename=HotkeyManagerSetup-{#AppVersion}
SetupIconFile=..\src\HotkeyManager\Assets\app.ico
UninstallDisplayIcon={app}\HotkeyManager.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "chinesesimplified"; MessagesFile: "lang\ChineseSimplified.isl"

[Tasks]
Name: "autostart"; Description: "开机自动启动"; GroupDescription: "附加任务:"; Flags: checkedonce
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\HotkeyManager.exe"; DestDir: "{app}"; Flags: ignoreversion
; 用户配置位于 %USERPROFILE%\.hotkeymanager.json，首次运行自动生成，安装/卸载均不触碰

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\HotkeyManager.exe"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\HotkeyManager.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "HotkeyManager"; ValueData: """{app}\HotkeyManager.exe"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\HotkeyManager.exe"; Description: "运行 {#AppName}"; Flags: nowait postinstall skipifsilent
