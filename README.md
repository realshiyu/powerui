# PowerUI

Windows 11 无线外设电量托盘工具。绿色数字托盘、磨砂玻璃弹出面板，不需要厂商控制中心常驻。

[下载便携版](https://github.com/realshiyu/powerui/releases) · [兼容设备](#兼容设备) · [构建](#从源码构建) · [报告问题](https://github.com/realshiyu/powerui/issues)

<img src="docs/images/tray.png" width="32" height="32" alt="绿色托盘数字样式示例">

图标样式示例，数字不代表实时电量。

## 功能

- 亮绿色底、深色加粗的托盘数字，悬停查看设备与电量。
- Windows 11 Acrylic 面板，跟随系统深浅色，提供不透明回退。
- 每 60 秒被动接收电量报告，单次最长等待 5 秒，不主动唤醒鼠标。
- 超过 5 分钟没有新电量时显示 `--`，详情保留上次读数及时间。
- 默认电量不高于 20% 时提醒一次；新读数高于 25% 后才允许下一轮提醒，重启不会重复提醒。
- 可调整提醒阈值（5%–90%）、关闭提醒，可选开机启动且默认关闭。
- 免安装、无第三方运行库、不安装服务、不要求管理员权限。

## 兼容设备

**目前不是通用的 2.4G 外设电量工具。** 不同品牌、型号与固件可能使用不同协议。

| 设备 | USB 接收器 VID:PID | 状态 |
| --- | --- | --- |
| 雷柏 VT3S MAX | `24AE:1421` | 已实机读取真实电量 |
| 雷柏 VT3 MAX | `24AE:1417` | 已实现协议匹配，未实机验证 |
| 雷柏 P5 PLUS 底座 | `24AE:4827` | 不支持，不向底座发送鼠标命令 |

当前仅处理单个匹配接收器；发现多个匹配接收器时拒绝自动选择。未覆盖的设备不会使用猜测命令尝试读取。

充电状态报告不当作新的百分比，避免错误显示为 `0%`。低电量通知实际送达、真实充电/睡眠/拔插转换、开机登录启动和多显示器仍待更多实机验证。已检查本机 125% 缩放的深浅色面板。

## 使用

1. 从 [Releases](https://github.com/realshiyu/powerui/releases) 下载 ZIP，解压到当前用户可写目录。
2. 启动 `PowerUI.exe`，左键托盘数字打开面板，右键菜单或设置中退出。
3. 初次启动若 Windows 将图标放在托盘折叠区，可将其拖到任务栏通知区域。

要求 Windows 11 x64 和系统自带的 .NET Framework 4.8 或更新版本。Windows 11 22H2 / build 22621 起支持本项目使用的 DWM Acrylic 属性；旧版本回退为不透明背景。开发版暂未签名，仅运行自己构建或可信来源的文件，不要关闭系统安全保护。

设置和上次电量保存在程序旁的 `PowerUI.state.xml`，上一份状态保存在 `.bak`。移动便携文件夹时保留它们；启用过开机启动的，移动程序后需重新开启。首次保存失败时请换用可写目录，而不是提权运行。

## 隐私与设备安全

应用运行时不联网、无遥测、不采集键盘按键或鼠标位移、不上传设备数据。不修改 DPI、按键、灯效或固件。常规刷新只读取厂商自定义电量报告，不发送设备命令。

开机启动仅在用户主动打开选项时写入当前用户的注册表 Run 项。运行数据、截图与日志不纳入源码或发布包。

## 从源码构建

在 Windows x64 的 PowerShell 中执行：

```powershell
git clone https://github.com/realshiyu/powerui.git
Set-Location powerui
.\build.ps1
.\package.ps1
```

脚本使用系统 .NET Framework C# 编译器，不需要 .NET SDK、NuGet 或 npm。若当前 PowerShell 策略不允许运行脚本，请遵循所在组织的脚本执行规则，不要修改整机安全策略。

产物位于 `artifacts/`。便携包仅包含主程序、运行配置、诊断程序、说明及许可文件，不包含开发机的设置或读数。CI 只验证构建与打包，不代表真实设备兼容性验证。

## 电量诊断

```powershell
.\artifacts\PowerUI.Probe.exe
.\artifacts\PowerUI.Probe.exe --listen
```

无参数只读枚举雷柏 HID 描述符；`--listen` 最多等待 8 秒，只解析电量报告。诊断不输出设备路径或序列号。

`--query` 是实验性诊断选项，会向严格匹配的鼠标接收器发送固件/电量只读请求。其 ACK 行为尚未验证通过，常规托盘应用不使用该路径，不推荐普通用户排障时运行。

排障先确认型号、连接方式、接收器是否可见，以及是否有有效电量报告。一次读取超时不能证明鼠标离线或不兼容。隔离/沙箱环境的设备访问拒绝也不意味着日常使用必须提权。

## 开发与贡献

项目采用 C# / WPF、NotifyIcon、Windows SetupAPI/HID 和 DWM。源文件在 `src/`，构建入口为 `build.ps1`，发布入口为 `package.ps1`。

新增设备前请阅读 [协议说明](docs/protocol.md) 和 [贡献指南](CONTRIBUTING.md)。安全问题见 [SECURITY.md](SECURITY.md)，版本变更见 [CHANGELOG.md](CHANGELOG.md)。

## 许可与声明

[MIT License](LICENSE)，Copyright (c) 2026 realshiyu。第三方资料与商标说明见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

这是独立社区项目，与雷柏 / RAPOO 无隶属、授权或背书关系；不包含厂商驱动、固件或官方网页驱动源码。
