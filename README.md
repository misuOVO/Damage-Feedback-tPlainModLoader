# 受伤反馈 — tPlainModLoader 移植

把 tModLoader 客户端 QoL 模组 **HP Awareness**（作者 Core）移植到 **tPlainModLoader / Terraria 1.4.5.8**，版本 1.0.0。

原模组：https://github.com/CoreMod/HP-Awareness
（本项目是运行时 Harmony 补丁版，不依赖 tModLoader。）

## 功能

| 功能 | 说明 |
|---|---|
| 受伤红屏 | 被打时屏幕边缘闪一下红色，然后淡出 |
| 低血量红框 | 血量低于阈值时屏幕边缘出现红色渐晕，**忽闪忽闪**，血越少越红 |
| 头顶血条 | 玩家头顶显示生命条（可切换回脚下位置） |
| 药水冷却提示 | 药水病结束时头顶弹出图标 + 音效 |
| Debuff 提示 | 获得 debuff 时头顶弹出图标，可选显示剩余时间 |
| 低血量音效 | 低血量时按间隔播放提示音（Bell / Heartbeat / Mana Chirp / Click） |
| 音乐闷音 | 低血量时压低当前音乐音量 |
| 灰视（近似） | 低血量时屏幕变暗（原版用 shader，这里用叠层近似） |

所有功能都可以在游戏内 **模组设置** 里单独开关、调滑条，改动即时生效并自动保存。

## 安装

1. 安装 tPlainModLoader（Terraria 1.4.5.8）；
2. 把 `HPAware.dll`、`info.json`、`loadConfig.json`、`ico.png` 放进：
   ```
   <Terraria>/Mods/HPAware/
   ```
3. 启动游戏，在模组设置里调整。

## 配置

配置文件：`<Terraria>/Mods/HPAware/config.json`
首次启动自动生成，设置页修改时自动写回。

## 从源码构建

需要 .NET SDK（使用自带 Roslyn 编译器）和游戏目录 `D:\steam\steamapps\common\Terraria`。

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

脚本会编译 `src/*.cs`，并把 DLL 和 `mod/*.json`、`assets/ico.png` 部署到游戏的 `Mods/HPAware`。

## 目录结构

```
src/
  ThisMod.cs          入口（tContentPatch.Mod）
  AwareHooks.cs       Harmony 挂点：Player.Update / Player.Hurt / DrawInterface
  AwareState.cs       状态机（计时器、低血量、闷音）
  AwareDraw.cs        绘制（圆角渐晕、血条、药水、debuff）
  PluginConfig.cs     配置读写（JSON）
  SettingPage.cs      游戏内模组设置页（ModSetting + 折叠分组 + 滚动条）
  SoundFx.cs          原版音效映射
mod/                  info.json / loadConfig.json 模板
assets/               ico.png / PotionReady.png
tools/make-icon.ps1   生成模组图标
build.ps1             编译 + 部署
```

## 实现要点

- tPlainModLoader 没有 `ModPlayer` / `ModConfig` / `Filters.Scene`，所以：
  - 用 Harmony 挂 `Player.Update(int)` 跑状态机、`Player.Hurt(...)` 触发受伤、`Main.DrawInterface_25_ResourceBars` 绘制；
  - 设置页通过继承 `tContentPatch.ModSetting` 注册，控件用加载器自带的 `UIItemSwitch` / `UIItemValueSlider`；
  - 红框不是 shader，而是运行时生成一张**圆角渐晕贴图**（SDF 计算，边缘强、向内渐隐），按屏幕尺寸拉伸；
  - 头顶 UI 用 `Main.GameViewMatrix.ZoomMatrix` 绘制，保证缩放/分辨率下位置正确。

## 致谢 / 许可

- 原模组 **HP Awareness** by **Core**（https://github.com/CoreMod/HP-Awareness），逻辑、默认值、贴图（PotionReady.png）均来自原项目；
- 本项目仅做 tPlainModLoader 平台移植。原仓库未声明开源许可证，如需 redistribution 请先征得原作者同意。
