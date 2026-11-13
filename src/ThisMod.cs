using System;
using System.Reflection;
using tContentPatch;
using tContentPatch.ModLoad;
using tContentPatch.Patch;

namespace HPAware
{
    /// <summary>
    /// tPlainModLoader 入口：HP Awareness 的移植版。
    ///
    /// 原版模组是 tModLoader 的客户端 QoL：
    ///   - 受伤时屏幕边缘红屏 / 低血量脉动 / 灰视
    ///   - 玩家头顶血条
    ///   - 药水冷却结束提示（图标 + 音效）
    ///   - 获得 debuff 时头顶弹出图标
    ///   - 低血量音乐闷音
    ///
    /// tPlainModLoader 没有 ModPlayer / ModConfig / Filters.Scene，
    /// 所以这里用 Harmony 直接挂原版方法，状态自己维护，配置走 JSON 文件。
    /// </summary>
    public class ThisMod : Mod
    {
        public override void Load(ModObject mo)
        {
            Log.Write("HPAware.Load key=" + (mo != null && mo.config != null ? mo.config.key : "?"));

            try
            {
                AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                    Log.Write("!!! HPAware 未处理异常: " + e.ExceptionObject);
            }
            catch
            {
            }

            PluginConfig.Load();
        }

        public override void AddPatch(IAddPatch addPatch)
        {
            AwareHooks.Apply(addPatch);
        }

        public override void Loaded()
        {
            Log.Write("HPAware.Loaded");
        }
    }
}
