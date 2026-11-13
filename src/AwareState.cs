using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace HPAware
{
    /// <summary>
    /// 原模组 HPSystemPlayer + MuffleSystem 的状态机移植。
    /// 每帧对本地玩家跑一次（Player.Update postfix），
    /// 受伤由 Player.Hurt postfix 通知。
    /// </summary>
    internal static class AwareState
    {
        public static float ShaderFade;
        public static int PotionTimer;
        public static int DebuffTimer;
        public static int BarTimer;
        public static float BarAlphaLerp;
        public static int DebuffToShow;
        public static readonly List<int> DebuffsToShow = new List<int>(64);

        private static int _lastLife;
        private static int _lastLowHpSoundTick;

        public static void Reset()
        {
            ShaderFade = 0f;
            PotionTimer = 0;
            DebuffTimer = 0;
            BarTimer = 0;
            BarAlphaLerp = 0f;
            DebuffToShow = 0;
            DebuffsToShow.Clear();
        }

        /// <summary>Player.Hurt 之后调用：受伤红屏 + 头顶血条。</summary>
        public static void OnHurt(Player p)
        {
            if (p == null || p.whoAmI != Main.myPlayer)
                return;

            PluginConfig M = PluginConfig.Instance;

            if (M.EnableHurtOverlay)
                ShaderFade = 1f;

            if (M.EnableHPBar && p.statLife <= p.statLifeMax2 * M.HPBarTrigger)
            {
                BarTimer = M.HPBarDelay;
                BarAlphaLerp = 1f;
            }
        }

        /// <summary>每帧对本地玩家调用。</summary>
        public static void Tick(Player p)
        {
            if (p == null || p.whoAmI != Main.myPlayer || Main.dedServ)
                return;

            PluginConfig M = PluginConfig.Instance;

            if (Main.gameMenu || p.dead || p.ghost)
            {
                if (ShaderFade > 0f || PotionTimer > 0 || DebuffTimer > 0 || BarAlphaLerp > 0f)
                    Reset();
                _lastLife = p.statLife;
                return;
            }

            if (_lastLife > 0 && p.statLife < _lastLife)
                OnHurt(p);
            _lastLife = p.statLife;

            UpdateDebuffs(p, M);
            UpdatePotion(p, M);
            UpdateHurtFade(M);
            UpdateLowHp(p, M);
            UpdateBar(p, M);
            UpdateMusicMuffle(p, M);
        }

        private static void UpdateDebuffs(Player p, PluginConfig M)
        {
            if (!M.EnableBuffVisual)
            {
                DebuffTimer = 0;
                DebuffsToShow.Clear();
                return;
            }

            for (int i = 0; i < p.buffType.Length; i++)
            {
                int type = p.buffType[i];
                int time = p.buffTime[i];

                if (type <= 0 || time <= 0)
                    continue;
                if (!Main.debuff[type])
                    continue;
                if (type < M.DoNotShow.Length && M.DoNotShow[type])
                    continue;

                if (!DebuffsToShow.Contains(type))
                {
                    DebuffToShow = type;
                    DebuffsToShow.Add(type);
                    DebuffTimer = M.BuffDelay;
                }
            }

            for (int i = DebuffsToShow.Count - 1; i >= 0; i--)
            {
                int type = DebuffsToShow[i];
                bool blacklisted = type < M.DoNotShow.Length && M.DoNotShow[type];
                if (p.FindBuffIndex(type) < 0 || blacklisted)
                    DebuffsToShow.RemoveAt(i);
            }

            if (DebuffTimer > 0 && M.BuffDelay <= 600)
                DebuffTimer = ClampI(DebuffTimer - 1, 0, M.BuffDelay);

            if (DebuffTimer <= 0 || DebuffsToShow.Count == 0)
                DebuffTimer = 0;
        }

        private static void UpdatePotion(Player p, PluginConfig M)
        {
            // potionDelay == 1 表示“药水病刚刚结束”
            if (p.potionDelay == 1)
            {
                if (M.EnablePSAudio)
                    SoundFx.PlayPotionReady();

                if (M.EnablePSVisual)
                    PotionTimer = M.PotionDelay;
            }

            if (PotionTimer > 0)
                PotionTimer = ClampI(PotionTimer - 1, 0, M.PotionDelay);
        }

        private static void UpdateHurtFade(PluginConfig M)
        {
            if (ShaderFade <= 0f)
                return;

            ShaderFade -= (float)Math.Round(M.HurtSpeed * 0.01, 2);
            ShaderFade = (float)Math.Round(ShaderFade, 2);
            if (ShaderFade < 0f)
                ShaderFade = 0f;
        }

        private static void UpdateLowHp(Player p, PluginConfig M)
        {
            if (!M.EnableLowHpAudio)
                return;

            if (p.statLife > p.statLifeMax2 * M.Overlaytrigger)
                return;

            int freq = Math.Max(1, M.LowHpSdFreq);
            int now = (int)Main.GameUpdateCount;

            if (now % freq != 0 || _lastLowHpSoundTick == now)
                return;

            _lastLowHpSoundTick = now;
            SoundFx.PlayLowHp(M.LowHpSound);
        }

        private static void UpdateBar(Player p, PluginConfig M)
        {
            if (BarTimer > 0 && M.HPBarDelay <= 600)
                BarTimer = ClampI(BarTimer - 1, 0, M.HPBarDelay);

            if (BarTimer <= 0 || p.statLife > p.statLifeMax2 * M.HPBarTrigger)
                BarAlphaLerp = MathHelper.Clamp(BarAlphaLerp - 0.1f, 0f, 1f);

            if (BarAlphaLerp <= 0f)
                BarAlphaLerp = 0f;
        }

        private static void UpdateMusicMuffle(Player p, PluginConfig M)
        {
            if (!M.EnableAudioMuffle)
                return;

            if (p.statLife > p.statLifeMax2 * M.MuffleTrigger)
                return;

            if (Main.curMusic <= 0 || Main.curMusic >= Main.musicFade.Length)
                return;

            float target = 1f - M.MuffleIntensity;
            if (Main.musicFade[Main.curMusic] > target)
                Main.musicFade[Main.curMusic] -= 0.01f;
            else
                Main.musicFade[Main.curMusic] = target;
        }

        private static int ClampI(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
