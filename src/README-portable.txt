PowerUI 0.1 - Windows 11 x64

项目：https://github.com/realshiyu/powerui
Copyright (c) 2026 realshiyu. MIT License，见 LICENSE。
独立社区项目，与雷柏 / RAPOO、罗技 / Logitech 无隶属或背书关系，见 THIRD_PARTY_NOTICES.md。

启动 PowerUI.exe。左键点击托盘数字打开面板，右键菜单或设置中可退出。
开机启动默认关闭，可在设置中开启。无需厂商软件常驻，无需管理员权限。

当前实测：雷柏 VT3S MAX，接收器 24AE:1421。
罗技 MX Master 3S 通过 Logi Bolt 046D:C548 和蓝牙 046D:B034 均已实测读取电量。
VT3 MAX 24AE:1417 未实测，不承诺其他型号。P5 PLUS 底座不在读取范围。
仅处理单个匹配接口，多个接收器、蓝牙与接收器同时匹配或多只 MX Master 3S 不会自动选择。

每 60 秒尝试读取，过期电量显示 --。充电状态不冒充新的百分比。
默认 <=20% 提醒一次，只有新读数 >25% 才重新允许下一轮提醒。
提醒阈值可选 5%-90%，恢复线随之设为阈值+5个百分点。

把便携文件放在可写目录。程序旁的 PowerUI.state.xml 和 .bak 保存本地设置、上次电量和提醒状态。
移动文件夹时保留它们；启用过开机启动的，移动后需重新开启。
不要为解决目录写权限而使用管理员身份运行。

需要 Windows 自带 .NET Framework 4.8 或更新版本，不需要额外安装 .NET SDK。
本程序为未签名开发版。低电量通知送达、真实充电/睡眠/拔插、开机登录仍待实机验收。

附带 PowerUI.Probe.exe：无参数只读枚举；--listen 最多被动等候8秒，输出真实电量。
--logitech 只读查询 Bolt 或 MX Master 3S 蓝牙型号和电量，不修改鼠标设置；查询可能让鼠标响应。
诊断不输出设备路径或序列号。
