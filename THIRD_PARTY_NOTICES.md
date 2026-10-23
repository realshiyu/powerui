# 第三方资料与商标说明

## 项目边界

项目当前没有打包第三方库、厂商驱动、固件或官方网页驱动 JavaScript。构建使用 Windows 自带的 .NET Framework 和系统 API；这些组件的权利归各自权利人，本项目的 MIT 许可不改变其许可。

界面使用已安装的 Windows 系统字体，没有嵌入或再分发字体文件。托盘数字由程序自行绘制，不使用厂商标识。

## 协议参考

设备映射及报告格式参考雷柏公开网页驱动，并通过实际设备报告核对。下列资料不是本项目的组成文件，也不因此被 MIT 重新授权：

- [雷柏官方下载入口](https://shop.rapoo.com/pages/software-download)
- [RAPOO HUB](https://hub.rapoo.com/home)
- 官方产品映射 `https://hub.rapoo.com/enc.data`
- 官方接收器映射 `https://hub.rapoo.com/enc-map.data`
- 调查时的主脚本 `https://hub.rapoo.com/assets/index-WEaXdsQS.js`，文件名可能更新

Windows 接口及材质依据 [Microsoft Learn](https://learn.microsoft.com/windows/) 文档实现。参考链接不表示第三方提供支持或背书。

罗技 HID++ 报告结构、Unified Battery 参数与名称分段查询参考以下开源实现中的协议事实，另行实现本项目的 Windows 通信代码；未复制或打包这些项目的源码：

- [Linux HID++ 驱动](https://github.com/torvalds/linux/blob/master/drivers/hid/hid-logitech-hidpp.c)（GPL-2.0-only）
- [Solaar HID++ 2.0 实现](https://github.com/pwr-Solaar/Solaar/blob/master/lib/logitech_receiver/hidpp20.py)（GPL-2.0-or-later）

VGN S99 电量命令与响应字段参考 [VGN 官方 Web HUB](https://drive.vgnlab.com.cn/)
的公开协议行为，并与 [BatteryMonitor 协议实现](https://github.com/JKWTCN/BatteryMonitor/blob/master/src/providers/hid/VgnHidProvider.cpp)
交叉核对。本项目独立实现 Windows 传输代码，不打包官方脚本或该项目代码。
本机接收器报告已验证百分比；充电标记尚未经真实充电转换验证。

## 商标

Windows、Microsoft、雷柏 / RAPOO、罗技 / Logitech、Logi Bolt、VGN 以及产品型号均属于其各自权利人，仅用于描述系统要求与兼容性。PowerUI 是独立项目，与这些厂商没有隶属、授权或背书关系。
