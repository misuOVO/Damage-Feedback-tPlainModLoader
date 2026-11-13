using System;
using System.Reflection;
using HarmonyLib;
using Terraria;

namespace HPAware
{
    /// <summary>
    /// Harmony 挂点：
    ///   Player.Update(int)  -> 每帧状态机
    ///   Player.Hurt(...)    -> 受伤触发
    ///   Main.DrawInterface  -> 绘制叠层与头顶 UI
    /// </summary>
    internal static class AwareHooks
    {
        private static Harmony _harmony;
        private static bool _applied;

        public static void Apply(tContentPatch.Patch.IAddPatch addPatch)
        {
            if (_applied)
                return;
            _applied = true;

            try
            {
                _harmony = new Harmony("HPAware.Patches");

                // ---- Player.Update(int) ----
                MethodInfo update = typeof(Player).GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(int) }, null);

                if (update != null)
                {
                    _harmony.Patch(update, postfix: new HarmonyMethod(
                        typeof(AwareHooks).GetMethod(nameof(PlayerUpdatePostfix),
                            BindingFlags.NonPublic | BindingFlags.Static)));
                    Log.Write("HPAware: 已挂 Player.Update");
                }
                else
                {
                    Log.Write("HPAware: 找不到 Player.Update(int)");
                }

                // ---- Player.Hurt(...) ----
                MethodInfo hurt = typeof(Player).GetMethod(
                    "Hurt",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(Terraria.DataStructures.PlayerDeathReason),
                        typeof(int), typeof(int), typeof(bool), typeof(bool),
                        typeof(bool), typeof(int), typeof(bool)
                    }, null);

                if (hurt != null)
                {
                    _harmony.Patch(hurt, postfix: new HarmonyMethod(
                        typeof(AwareHooks).GetMethod(nameof(PlayerHurtPostfix),
                            BindingFlags.NonPublic | BindingFlags.Static)));
                    Log.Write("HPAware: 已挂 Player.Hurt");
                }
                else
                {
                    Log.Write("HPAware: 找不到 Player.Hurt(...)（掉血检测仍可用）");
                }

                // ---- 界面绘制点 ----
                // 注意：原版 DrawInterface_25_ResourceBars / DrawInterface 都是
                // Main 的【实例】方法（private / protected），不是 static。
                // 优先 ResourceBars 图层：原版在这里已经 Begin 过 spriteBatch。
                MethodInfo draw = typeof(Main).GetMethod(
                    "DrawInterface_25_ResourceBars",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);

                if (draw == null)
                {
                    draw = typeof(Main).GetMethod(
                        "DrawInterface",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new[] { typeof(Microsoft.Xna.Framework.GameTime) }, null);
                }

                if (draw != null)
                {
                    _harmony.Patch(draw, postfix: new HarmonyMethod(
                        typeof(AwareHooks).GetMethod(nameof(DrawInterfacePostfix),
                            BindingFlags.NonPublic | BindingFlags.Static)));
                    Log.Write("HPAware: 已挂绘制点 " + draw.Name);
                }
                else
                {
                    Log.Write("HPAware: 找不到绘制点");
                }
            }
            catch (Exception e)
            {
                Log.Write("HPAware 挂载失败: " + e);
            }
        }

        private static void PlayerUpdatePostfix(Player __instance)
        {
            try
            {
                if (__instance == null || __instance.whoAmI != Main.myPlayer)
                    return;

                AwareState.Tick(__instance);
            }
            catch (Exception e)
            {
                Log.Write("HPAware Tick 异常: " + e);
            }
        }

        private static void PlayerHurtPostfix(Player __instance, double __result)
        {
            try
            {
                if (__instance == null || __result <= 0.0)
                    return;

                AwareState.OnHurt(__instance);
            }
            catch
            {
            }
        }

        private static void DrawInterfacePostfix()
        {
            try
            {
                AwareDraw.DrawAll(Main.spriteBatch);
            }
            catch (Exception e)
            {
                Log.Write("HPAware Draw 异常: " + e);
            }
        }
    }
}
