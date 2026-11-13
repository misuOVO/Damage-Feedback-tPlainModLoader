using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace HPAware
{
    /// <summary>
    /// 原模组用的全是原版音效（Terraria/Sounds/...），
    /// 这里映射到 SoundID 并用 volume/pitch 复现。
    /// 注意：SoundID 里有一部分字段是 int（老式音效），另一部分是 LegacySoundStyle。
    /// </summary>
    internal static class SoundFx
    {
        public static void PlayPotionReady()
        {
            PlayStyle(SoundID.Item3, 1f, 0.3f);
        }

        public static void PlayLowHp(string which)
        {
            switch (which)
            {
                case "Heartbeat":
                    PlayStyle(SoundID.DeerclopsStep, 1f, -1f);
                    break;
                case "Mana Chirp":
                    PlayInt(SoundID.MaxMana, 1f, -0.6f);
                    break;
                case "Click":
                    PlayInt(SoundID.Drip, 1f, 0.5f);
                    break;
                case "Bell (No Pitch)":
                    PlayStyle(SoundID.Item35, 1f, 0f);
                    break;
                default: // Bell
                    PlayStyle(SoundID.Item35, 1f, 0.8f);
                    break;
            }
        }

        public static void PlayAddToBlacklist()
        {
            PlayStyle(SoundID.Item20, 1f, 0f);
        }

        private static Vector2 LocalPos()
        {
            return Main.LocalPlayer != null ? Main.LocalPlayer.Center : Main.screenPosition;
        }

        private static void PlayStyle(LegacySoundStyle style, float volume, float pitch)
        {
            try
            {
                SoundEngine.PlaySound(style, LocalPos(), volume, pitch);
            }
            catch
            {
            }
        }

        private static void PlayInt(int type, float volume, float pitch)
        {
            try
            {
                Vector2 pos = LocalPos();
                SoundEngine.PlaySound(type, (int)pos.X, (int)pos.Y, 0, volume, pitch);
            }
            catch
            {
            }
        }
    }
}
