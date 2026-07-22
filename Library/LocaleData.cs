  //\\   OmenMon: Hardware Monitoring & Control Utility
 //  \\  Copyright © 2023-2024 Piotr Szczepański * License: GPL3
     //  https://omenmon.github.io/

using System;
using System.Collections.Generic;
using OmenMon.Library;

namespace OmenMon.Library.Locale {

    // Defines locale constants, variables, and structures
    // for subsequent use by the localization routines
    public abstract class LocaleData {

        // Language list
        public enum Language : int {
                Fallback,  // Default fallback
                Override   // Loaded from file
            }

        // Default fallback message data
        protected Dictionary<string, string> msgFallback
            = new Dictionary<string, string>() {

                // CLI
                ["CliHeader"] = "硬件监控与控制工具",
                ["CliHeaderVersion"] = "版本",
                ["CliActionGet"] = "-",
                ["CliActionSet"] = "+",
                ["CliDetailsFollow"] = Conv.GetChar(Conv.SpecialChar.ArrowDown),
                ["CliStateOn"] = "是",
                ["CliStateOff"] = "否",
                ["CliTranslated"] = "", // Only filled out for translations

                // CLI: BIOS
                ["CliBios"] = "BIOS",
                ["CliBiosAdapter"] = "智能电源适配器状态",
                ["CliBiosAnim"] = "LED 动画表",
                ["CliBiosBacklight"] = "键盘背光",
                ["CliBiosBornDate"] = "出厂日期",
                ["CliBiosBornDateNote"] = "年年年年月月日日",
                ["CliBiosColor"] = "键盘背光颜色表",
                ["CliBiosColorZones"] = "分区",
                ["CliBiosCpuPowerLimit1"] = "CPU 功耗限制 1",
                ["CliBiosCpuPowerLimit4"] = "CPU 功耗限制 4",
                ["CliBiosCpuPowerLimitWithGpu"] = "与 GPU 并发时的 CPU 功耗限制",
                ["CliBiosFanCount"] = "风扇数量",
                ["CliBiosFanLevelN"] = "风扇 #{0} 档位",
                ["CliBiosFanMax"] = "风扇最大转速",
                ["CliBiosFanMode"] = "风扇模式",
                ["CliBiosFanTable"] = "风扇转速档位表",
                ["CliBiosFanTableFans"] = "风扇",
                ["CliBiosFanTableLevels"] = "档位",
                ["CliBiosFanType"] = "风扇类型",
                ["CliBiosFanTypeN"] = "风扇 #{0} 类型",
                ["CliBiosGpuMode"] = "显卡模式（旧版）",
                ["CliBiosGpuPower"] = "GPU 功耗设置",
                ["CliBiosGpuPowerCustomTgp"] = "GPU 自定义总功耗（cTGP）",
                ["CliBiosGpuPowerDState"] = "GPU 设备电源状态（DState）",
                ["CliBiosGpuPowerPeakTemperature"] = "GPU 峰值温度传感器阈值",
                ["CliBiosGpuPowerPpab"] = "GPU 性能 AI 加速（PPAB）",
                ["CliBiosHasBacklight"] = "键盘背光支持",
                ["CliBiosHasMemoryOverclock"] = "内存超频支持",
                ["CliBiosHasOverclock"] = "超频支持",
                ["CliBiosHasUndervolt"] = "BIOS 降压支持",
                ["CliBiosIdle"] = "空闲模式",
                ["CliBiosKbdType"] = "键盘类型",
                ["CliBiosSystem"] = "系统设计数据",
                ["CliBiosSystemBiosOc"] = "BIOS 定义的超频",
                ["CliBiosSystemDefaultCpuPowerLimit4"] = "默认 CPU 功耗限制 4",
                ["CliBiosSystemDefaultCpuPowerLimitWithGpu"] = "默认 CPU 与 GPU 并发功耗限制",
                ["CliBiosSystemDefaultCpuPowerLimitWithGpuNote"] = "Cybug 23C1 及以后机型",
                ["CliBiosSystemGpuModeSwitch"] = "显卡模式切换支持",
                ["CliBiosSystemStatusFlags"] = "状态标志",
                ["CliBiosSystemSupportFlags"] = "支持标志",
                ["CliBiosSystemThermalPolicyVersion"] = "散热策略版本",
                ["CliBiosSystemUnknown2"] = "未知字节",
                ["CliBiosSystemUnknown2Note"] = "观测到的常量 0x35 = 53",
                ["CliBiosTemp"] = "温度",
                ["CliBiosThrottling"] = "热降频状态",
                ["CliBiosXmp"] = "内存 XMP 配置",

                // CLI: Embedded Controller
                ["CliEc"] = "嵌入式控制器",
                ["CliEcMon"] = "嵌入式控制器监视",
                ["CliEcByte"] = "字节",
                ["CliEcRegister"] = "寄存器",
                ["CliEcWord"] = "字",
                ["CliEcWordNote"] = "（小端序）",

                // CLI: Program
                ["CliProg"] = "程序",
                ["CliProgCallback"] = "回调",
                ["CliProgName"] = "程序",
                ["CliProgFanMode"] = "风扇模式",
                ["CliProgGpuPower"] = "GPU 功耗",

                // CLI: Task
                ["CliTask"] = "任务计划",
                ["CliTaskGui"] = "用户登录时自动运行",
                ["CliTaskKey"] = "拦截 Omen 键",
                ["CliTaskMux"] = "Advanced Optimus 缺陷修复",

                // CLI: Usage
                ["CliUsage"] = "用法信息",
                ["CliUsageText"] =
                    "用法: {0} [-<参数1> [...] [-<参数N> [...]]]" + Environment.NewLine +
                    "其中:" + Environment.NewLine +
                    "<参数#>" + Environment.NewLine +
                    "  -Bios                     运行所有仅读取信息的 BIOS 操作" + Environment.NewLine +
                    "  -Bios <BiosOp>[=<Data>]+  执行一个或多个 BIOS 操作，可带参数" + Environment.NewLine +
                    "  -Ec                       以表格形式获取所有嵌入式控制器寄存器的值" + Environment.NewLine +
                    "  -Ec [<Reg>][=<Byte>]+     获取或设置一个或多个指定寄存器的字节值" + Environment.NewLine +
                    "  -Ec [<Reg>(2)][=<Word>]+  获取或设置一对或多对连续寄存器的字值" + Environment.NewLine +
                    "  -EcMon [文件名]           监视所有寄存器的变化并报告，可选保存到文件" + Environment.NewLine +
                    "  -Prog                     列出从配置文件加载的所有风扇控制程序" + Environment.NewLine +
                    "  -Prog <名称>              运行指定的风扇控制程序" + Environment.NewLine +
                    "  -Run <任务名> [<参数>]    运行指定任务（无头模式，无控制台输出）" + Environment.NewLine +
                    "  -Task                     检查所有计划任务的状态" + Environment.NewLine +
                    "  -Task <任务名>[=<标志>]+  启用或禁用计划任务" + Environment.NewLine +
                    "  -?|-H|[-]-Help|[-]-Usage  显示用法信息" + Environment.NewLine +
                    "<BiosOp>" + Environment.NewLine +
                    "  Cpu:PL1=<Byte> Cpu:PL4=<Byte> Cpu:PLGpu=<Byte> Gpu[=<GpuPreset>] GpuMode[=<GpuMode>] Xmp=<Flag>" + Environment.NewLine +
                    "  FanCount FanLevel[=<FanLevel>] FanMax[=<Flag>] FanMode=<FanMode> FanTable[=<FanTable>] FanType" + Environment.NewLine +
                    "  Idle[=<Flag>] Temp Throttling BornDate System Adapter HasOverclock HasMemoryOverclock HasUndervolt" + Environment.NewLine +
                    "  KbdType HasBacklight Backlight[=<Flag>] Color[=<Color>] Anim[=<ByteArray>]" + Environment.NewLine +
                    "<Data>" + Environment.NewLine +
                    "{1}" +
                    "参数不区分大小写。任何参数都可以出现任意多次。",

                // GUI
                ["GuiAlreadyRunning"] = "已在后台运行：点击通知区域图标，或运行 OmenMon -Usage 查看命令行参数",
                ["GuiBtnDel"] = Conv.GetChar(Conv.SpecialChar.HeavyMultiplication),
                ["GuiBtnSet"] = Conv.GetChar(Conv.SpecialChar.HeavyCheckmark),
                ["GuiPromptReboot"] = "需要重启系统\r\n更改才能生效\r\n\r\n现在重启？",
                ["GuiTranslated"] = "", // Only filled out for translations

                // GUI: About (doubles as an error form)
                ["GuiAboutTitle"] = "关于 OmenMon",
                ["GuiAboutTitleError"] = "OmenMon 错误",
                ["GuiAboutCaption"] = "Omen 硬件监控与控制",
                ["GuiAboutText"] = "{\\rtf1\\ansi 通过 WMI BIOS 和嵌入式控制器监控温度、控制风扇转速。轻量，可在后台低占用运行，同时支持命令行模式。}",
                ["GuiAboutTextErrorPrefix"] = "{\\rtf1\\ansi\\deff0{\\colortbl;\\red255\\green0\\blue0;}\\cf1",
                ["GuiAboutTextErrorSuffix"] = "}",

                // GUI: Main
                ["GuiMainFan"] = "风扇监控与控制",
                ["GuiMainFan0"] = "CPU",
                ["GuiMainFan1"] = "GPU",
                ["GuiMainFanAuto"] = "自动",
                ["GuiMainFanConst"] = "恒速",
                ["GuiMainFanMax"] = "最大",
                ["GuiMainFanProg"] = "程序",
                ["GuiMainFanProgSet"] = "设置风扇程序",
                ["GuiMainFanProgSetNoSel"] = "未选择程序",
                ["GuiMainFanOff"] = "关闭",
                ["GuiMainKbd"] = "键盘背光与颜色",
                ["GuiMainKbdColorPickLeft"] = "左区颜色",
                ["GuiMainKbdColorPickMiddle"] = "中区颜色",
                ["GuiMainKbdColorPickRight"] = "右区颜色",
                ["GuiMainKbdColorPickWasd"] = "WASD 键颜色",
                ["GuiMainKbdColorPresetAdd"] = "保存预设",
                ["GuiMainKbdColorPresetAddValueDefault"] = "新建预设",
                ["GuiMainKbdColorPresetDel"] = "删除预设",
                ["GuiMainKbdColorPresetDelConfirm"] = "确定吗？",
                ["GuiMainKbdColorPresetDelNoSel"] = "未选择预设",
                ["GuiMainKbdColorPresetDelPrompt"] = "删除",
                ["GuiMainSys"] = "系统状态与信息",
                ["GuiMainSysAdapterNotSupported"] = Conv.RTF_CF1 + "电源状态未知",
                ["GuiMainSysAdapterMeetsRequirement"] = Conv.RTF_CF3 + "电源正常",
                ["GuiMainSysAdapterBelowRequirement"] = Conv.RTF_CF4 + "电源功率不足",
                ["GuiMainSysAdapterBatteryPower"] = Conv.RTF_CF1 + "使用电池供电",
                ["GuiMainSysAdapterNotFunctioning"] = Conv.RTF_CF4 + "电源故障",
                ["GuiMainSysAdapterError"] = Conv.RTF_CF4 + "电源错误",
                ["GuiMainSysBorn"] = "*",
                ["GuiMainSysGpu"] = "GPU",
                ["GuiMainSysGpuPpab"] = "PPAB",
                ["GuiMainSysGpuCustomTgp"] = "cTGP",
                ["GuiMainSysGpuDState"] = "DState",
                ["GuiMainSysThrottlingUnknown"] = Conv.RTF_CF1 + "",
                ["GuiMainSysThrottlingDefault"] = Conv.RTF_CF5 + "未降频",
                ["GuiMainSysThrottlingOn"] = Conv.RTF_CF4 + "降频中",
                ["GuiMainSysMsgWelcome"] = "欢迎使用！",
                ["GuiMainTitle"] = "Omen 硬件监控与控制",
                ["GuiMainTmp"] = "温度传感器读数",
                ["GuiMainTmpCPUT"] = "CPUT",
                ["GuiMainTmpGPTM"] = "GPTM",
                ["GuiMainTmpIRSN"] = "IRSN",
                ["GuiMainTmpRTMP"] = "RTMP",
                ["GuiMainTmpTMP1"] = "TMP1",
                ["GuiMainTmpTNT2"] = "TNT2",
                ["GuiMainTmpTNT3"] = "TNT3",
                ["GuiMainTmpTNT4"] = "TNT4",
                ["GuiMainTmpTNT5"] = "TNT5",

                // GUI: Menu
                ["GuiMenuSubFan"] = "风扇",
                ["GuiMenuActFanMax"] = "最大转速",
                ["GuiMenuActFanModeCool"] = "凉爽",
                ["GuiMenuActFanModeDefault"] = "默认",
                ["GuiMenuActFanModeL0"] = "旧版档位 0",
                ["GuiMenuActFanModeL1"] = "旧版档位 1",
                ["GuiMenuActFanModeL2"] = "旧版档位 2",
                ["GuiMenuActFanModeL3"] = "旧版档位 3",
                ["GuiMenuActFanModeL4"] = "旧版档位 4",
                ["GuiMenuActFanModeL5"] = "旧版档位 5",
                ["GuiMenuActFanModeL6"] = "旧版档位 6",
                ["GuiMenuActFanModeL7"] = "旧版档位 7",
                ["GuiMenuActFanModeL8"] = "旧版档位 8",
                ["GuiMenuActFanModeLegacyCool"] = "旧版凉爽",
                ["GuiMenuActFanModeLegacyDefault"] = "旧版默认",
                ["GuiMenuActFanModeLegacyExtreme"] = "旧版极限",
                ["GuiMenuActFanModeLegacyPerformance"] = "旧版性能",
                ["GuiMenuActFanModeLegacyQuiet"] = "旧版静音",
                ["GuiMenuActFanModePerformance"] = "性能",
                ["GuiMenuActFanOff"] = "关闭",
                ["GuiMenuSubGpu"] = "显卡",
                ["GuiMenuActGpuDisplayColor"] = "重新加载颜色配置",
                ["GuiMenuActGpuDisplayOff"] = "关闭显示器",
                ["GuiMenuActGpuPowerMin"] = "基础功耗",
                ["GuiMenuActGpuPowerMed"] = "额外功耗",
                ["GuiMenuActGpuPowerMax"] = "额外功耗加加速",
                ["GuiMenuActGpuRefreshHigh"] = "高刷新率",
                ["GuiMenuActGpuRefreshLow"] = "标准刷新率",
                ["GuiMenuActGpuModeDiscrete"] = "独显直连",
                ["GuiMenuActGpuModeOptimus"] = "Optimus 软切换",
                ["GuiMenuSubKbd"] = "键盘",
                ["GuiMenuActKbdBacklight"] = "背光",
                ["GuiMenuActKbdColorPresetDefaultApp"] = "OmenMon 冷色",
                ["GuiMenuActKbdColorPresetDefaultOem"] = "出厂默认",
                ["GuiMenuSubSet"] = "设置",
                ["GuiMenuActSetStayTop"] = "窗口置顶",
                ["GuiMenuActSetIconDyn"] = "动态图标",
                ["GuiMenuActSetIconDynBg"] = "动态背景",
                ["GuiMenuActSetTaskGui"] = "开机自启动",
                ["GuiMenuActSetAutoconfig"] = "启动时应用设置",
                ["GuiMenuActSetTaskKey"] = "拦截 Omen 键",
                ["GuiMenuActSetTaskMux"] = "Advanced Optimus 修复",
                ["GuiMenuActToggleFormMain"] = "显示监控窗口",
                ["GuiMenuActToggleFormMainHide"] = "隐藏监控窗口",
                ["GuiMenuActExit"] = "退出",

                // GUI: Tooltips
                ["GuiTipBtnAccept"] = "确认并继续",
                ["GuiTipBtnCancel"] = "取消并关闭对话框",
                ["GuiTipFan0Cap"] = "左侧显示第一个（CPU）风扇的读数",
                ["GuiTipFan1Cap"] = "右侧显示第二个（GPU）风扇的读数",
                ["GuiTipFanUnitVal"] = "风扇转速以每分钟转数（rpm）计量",
                ["GuiTipFan0Val"] = "CPU 风扇实时转速读数 [rpm]",
                ["GuiTipFan1Val"] = "GPU 风扇实时转速读数 [rpm]",
                ["GuiTipFanUnitRte"] = "风扇相对转速以百分比（%）计量",
                ["GuiTipFan0Rte"] = "CPU 风扇相对转速 [%]",
                ["GuiTipFan0RteBar"] = "以进度条显示的 CPU 风扇相对转速",
                ["GuiTipFan1Rte"] = "GPU 风扇相对转速 [%]",
                ["GuiTipFan1RteBar"] = " 以进度条显示的 GPU 风扇相对转速" + Environment.NewLine + " 注意起点在右侧",
                ["GuiTipFan0Lvl"] = "CPU 风扇档位 [krpm]" + Environment.NewLine + "自定义转速：拖动滑块" + Environment.NewLine + "再点击按钮应用",
                ["GuiTipFan1Lvl"] = "GPU 风扇档位 [krpm]" + Environment.NewLine + "自定义转速：拖动滑块" + Environment.NewLine + "再点击按钮应用",
                ["GuiTipFanCountdown"] = "如适用，此处显示 BIOS 恢复自动默认设置" + Environment.NewLine + "之前的倒计时" + Environment.NewLine + "选择“恒速”可防止计时结束",
                ["GuiTipFanProg"] = "风扇程序" + Environment.NewLine + "转速将按你的设定" + Environment.NewLine + "随温度变化",
                ["GuiTipFanProgCmb"] = "从下拉列表中选择一个风扇程序",
                ["GuiTipFanAuto"] = "自动模式（默认设置）",
                ["GuiTipFanMode"] = "从下拉列表中选择一个风扇模式",
                ["GuiTipFanConst"] = "恒速模式" + Environment.NewLine + "用滑块设置每个风扇的档位",
                ["GuiTipFanMax"] = "最大转速模式" + Environment.NewLine + "风扇以最大转速运行" + Environment.NewLine + "（5,500 与 5,700 rpm）",
                ["GuiTipFanOff"] = "关闭风扇" + Environment.NewLine + "完全停止风扇",
                ["GuiTipFanSet"] = "点击应用当前设置" + Environment.NewLine + "设置有变更时按钮会高亮",
                ["GuiTipKbdBacklight"] = "开关键盘背光",
                ["GuiTipKbdColorPreset"] = "从下拉框中选择要应用的" + Environment.NewLine + "颜色预设",
                ["GuiTipKbdColorPresetDel"] = "删除当前选中的预设",
                ["GuiTipKbdColorPresetSet"] = "将当前设置保存为预设",
                ["GuiTipKbdColorVal"] = "用此参数以十六进制值调整颜色" + Environment.NewLine + "命令行设置颜色：OmenMon -Bios Color=<参数>",
                ["GuiTipKbdPic"] = "点击某个分区可用取色器更改该区颜色" + Environment.NewLine + "更改会立即生效",
                ["GuiTipSys"] = "此处显示系统状态信息",
                ["GuiTipTmpCPUT"] = "CPU 温度",
                ["GuiTipTmpGPTM"] = "GPU 温度",
                ["GuiTipTmpBIOS"] = "BIOS 报告的温度" + Environment.NewLine + "观测值比其他任何" + Environment.NewLine + "传感器都低得多",
                ["GuiTipTmpIRSN"] = "红外传感器温度",
                ["GuiTipTmpRTMP"] = "平台控制器中枢（PCH）温度",
                ["GuiTipTmpTMP1"] = "内存温度",
                ["GuiTipTmpTNT2"] = "含义未知",
                ["GuiTipTmpTNT3"] = "存储",
                ["GuiTipTmpTNT4"] = "存储",
                ["GuiTipTmpTNT5"] = "含义未知",
                ["GuiTipTmpUnknown"] = "自定义传感器",
                ["GuiTipTxtInput"] = "请输入数值",

                // Data formats
                ["DataTypeBool"] = "<Flag>",
                ["DataSyntaxBool"] = "<On|True|Yes|1> | <Off|False|No|0>",

                ["DataTypeByte"] = "<Byte>",
                ["DataSyntaxByte"] = "<0-255|0x00-0xFF|0b00000000-0b11111111>",

                ["DataTypeByteArray"] = "<ByteArray>",
                ["DataSyntaxByteArray"] = "<00-FF>+",

                ["DataTypeColor4"] = "<Color>",
                ["DataSyntaxColor4"] = "<PresetName> | <RGB0>:<RGB1>:<RGB2>:<RGB3> (<RGB#>: 000000-FFFFFF)",

                ["DataTypeFanLevel"] = "<FanLevel>",
                ["DataSyntaxFanLevel"] = "<Fan1>,<Fan2> (<Fan#>: 0-255|0x00-0xFF|0b00000000-0b11111111)",

                ["DataTypeFanMode"] = "<FanMode>",
                ["DataSyntaxFanMode"] = "<FanModeId|0-255|0x00-0xFF|0b...> (<FanModeId>: Default|Performance|Cool|L#, <#>: 0-8)",

                ["DataTypeFanTable"] = "<FanTable>",
                ["DataSyntaxFanTable"] = "<Fan1>,<Fan2>,<Temp>[:...[:...]] (<Fan#>, <Temp>: <Byte>)",

                ["DataTypeGpuMode"] = "<GpuMode>",
                ["DataSyntaxGpuMode"] = "<GpuModeId|0-255|0x00-0xFF|0b...> (<GpuModeId>: Hybrid|Discrete|Optimus)",

                ["DataTypeGpuPowerLevel"] = "<GpuPreset>",
                ["DataSyntaxGpuPowerLevel"] = "Max[imum] | Med[ium]|Mid[dle] | Min[imum]",

                ["DataTypeReg"] = "<Reg>",
                ["DataSyntaxReg"] = "<NAME|0-255|0x00-0xFF|0b00000000-0b11111111>",
                ["DataSyntaxOrTwo"] = "[(2)]",

                ["DataTypeTName"] = "<TName>",
                ["DataSyntaxTName"] = "Autorun (GUI) | Key (Omen Key Capture) | Mux (Advanced Optimus Fix)",

                ["DataTypeWord"] = "<Word>",
                ["DataSyntaxWord"] = "<0-65535|0x0000-0xFFFF|0b0000000000000000-0b1111111111111111>",

                // Error messages
                ["ErrArgUnknown"] = "未知参数",
                ["ErrBiosCall"] = "BIOS 调用失败",
                ["ErrBiosInit"] = "初始化 BIOS 控制失败。请确认你使用的是兼容的 HP 系统，并已安装 ACPI\\PNP0C14 驱动。",
                ["ErrBiosNull"] = "实例化 BIOS 控制失败",
                ["ErrBiosSend"] = "发起 BIOS 调用失败",
                ["ErrBiosSendCommand"] = "命令不可用",
                ["ErrBiosSendSize"] = "输入或输出大小过小",
                ["ErrBiosSendUnknown"] = "BIOS 返回未知响应：{0}",
                ["ErrConfigLoad"] = "加载配置数据失败",
                ["ErrConfigSave"] = "保存配置数据失败",
                ["ErrEcInit"] = "初始化嵌入式控制器失败",
                ["ErrEcLock"] = "获取嵌入式控制器独占锁失败",
                ["ErrEcNull"] = "实例化嵌入式控制器失败",
                ["ErrFileSave"] = "保存文件失败",
                ["ErrLocaleNull"] = "实例化本地化消息系统失败",
                ["ErrLocaleLoad"] = "从外部文件加载本地化消息失败",
                ["ErrNeedRegisterRead"] = "需要指定要读取的寄存器",
                ["ErrNeedRegisterWrite"] = "需要指定要写入的寄存器",
                ["ErrNeedValueBool"] = "需要一个布尔标志",
                ["ErrNeedValueByte"] = "需要一个要设置的字节值",
                ["ErrNeedValueByteArray"] = "需要一个要设置的字节数组值",
                ["ErrNeedValueColor4"] = "需要四个颜色值组成的数组",
                ["ErrNeedValueFanLevel"] = "需要一对风扇转速档位",
                ["ErrNeedValueFanMode"] = "需要一个风扇模式",
                ["ErrNeedValueFanTable"] = "需要一个风扇表条目数组",
                ["ErrNeedValueGpuMode"] = "需要一个 GPU 模式",
                ["ErrNeedValueGpuPowerLevel"] = "需要一个 GPU 功耗预设",
                ["ErrNeedValueWord"] = "需要一个要设置的字值",
                ["ErrNotImplemented"] = "尚未实现",
                ["ErrProgName"] = "没有这个程序",
                ["ErrProgNone"] = "未配置任何程序",
                ["ErrUnexpected"] = "异常",
                ["ErrUnexpectedReally"] = "无可用详情",

                // Program
                ["Prog"] = "程序",
                ["ProgAlt"] = "[备用]",
                ["ProgEnd"] = "程序已结束",
                ["ProgFans"] = "风扇",
                ["ProgLvl"] = "档位",
                ["ProgT"] = "温度",
                ["ProgSubMax"] = "最大",

                // Units
                ["UnitFrequency"] = "Hz",
                ["UnitPercent"] = "%",
                ["UnitPower"] = "W",
                ["UnitRotationRate"] = "rpm",
                ["UnitRotationRate_CustomFont"] = Conv.GetChar(Conv.SpecialChar.Prime1) + Conv.GetChar(Conv.SpecialChar.SupMinus) + Conv.GetChar(Conv.SpecialChar.Sup1),
                ["UnitTemperature"] = "°C",
                ["UnitTemperature_CustomFont"] = Conv.GetChar(Conv.SpecialChar.DegreeCelsius),
                ["UnitTimeSecond_CustomFont"] = Conv.GetChar(Conv.SpecialChar.SpacePerEm6) + Conv.GetChar(Conv.SpecialChar.Prime2),

                // XML
                ["_ConfigXmlTemplate"] =
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + Environment.NewLine +
                    "<OmenMon>" + Environment.NewLine +
                    "    <!-- Automatically generated because no prior configuration file was found." + Environment.NewLine +
                    "         A version annotated with extensive comments is distributed with OmenMon.  -->" + Environment.NewLine +
                    "    <Config/>" + Environment.NewLine +
                    "    <Messages>" + Environment.NewLine +
                    "    </Messages>" + Environment.NewLine +
                    "</OmenMon>" + Environment.NewLine,

                // Language identifier
                ["_Language"] = "Fallback"

        };

    }

}
