# ClipHistory

Windows 本地历史剪贴板软件。目前已达到初步可用里程碑。

## 立即运行

开发环境可双击项目根目录的 `Run-ClipHistory.cmd`。

生成普通 Windows 用户可直接运行的版本：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Publish-Windows.ps1
```

发布程序位于 `artifacts/win-x64/ClipHistory.App.exe`，自带 .NET 运行时。

生成带开始菜单、可选桌面快捷方式和卸载入口的安装包：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/Build-Installer.ps1
```

安装包位于 `artifacts/installer/ClipHistory-Setup-win-x64.exe`。

运行后可以：

- 以悬浮面板形式固定在屏幕顶部居中，鼠标靠近自动展开、移开自动收起；
- 自动记录文字、图片、文件和文件夹；
- 按时间倒序查看，置顶内容优先；
- 搜索文字、文件名和路径，并按类型筛选；
- 点击“再次复制”后到目标软件按 `Ctrl + V`；
- 置顶、取消置顶、删除并在 5 秒内撤销；
- 暂停或恢复剪贴板记录；
- 关闭并重新启动后保留历史数据。
- 关闭窗口后继续在系统托盘运行；
- 使用可配置的全局快捷键显示窗口；
- 可选开机自动启动；
- 设置 1、3、5 天保存期限；
- 简体中文、English 或跟随 Windows，并立即切换；
- 二次确认后清空普通记录或全部记录。

当前数据保存在 `%LOCALAPPDATA%\ClipHistory\`，普通记录默认保存 3 天，置顶记录不会自动过期。

## 当前里程碑之后的工作

- 正式安装/卸载向导与应用图标

详细规范和计划见 `docs/README.md`。
