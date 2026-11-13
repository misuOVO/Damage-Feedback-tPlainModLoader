using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Terraria;
using Terraria.ID;
using tContentPatch;

namespace HPAware
{
    /// <summary>
    /// 对应原模组的 Modconfig。tPlainModLoader 没有 ModConfig 系统，
    /// 所以这里直接读写 Mods\HPAware\config.json，
    /// 同时通过 HPAwareSetting 暴露到游戏内“模组设置”页面。
    /// </summary>
    public class PluginConfig
    {
        // ---------------- 受伤 ----------------
        public bool EnableHurtOverlay = true;
        public string HurtOverlayType = "NewHPOverlay";   // NewHPOverlay / HPOverlay / HPOverlayFlat
        public int[] HurtColor = { 255, 0, 0 };
        public bool HurtUseDarkColors = false;
        public float HurtAlpha = 1.5f;
        public int HurtSpeed = 6;

        // ---------------- 红框外观（圆角 / 渐晕 / 闪烁） ----------------
        public float OverlayRoundness = 0.6f;    // 0 = 直角，1 = 很圆
        public float OverlayThickness = 0.45f;   // 渐变从边缘往内延伸的比例
        public float OverlayFlicker = 0.6f;      // 0 = 不闪，1 = 闪得最明显

        // ---------------- 低血量 ----------------
        public bool EnableLowHpOverlay = true;
        public bool ClassicLowHpOverlay = false;
        public int[] LowHpColor = { 255, 0, 0 };
        public bool LowHpUseDarkColors = false;
        public float Overlaytrigger = 0.25f;
        public float LowHpAlpha = 1f;
        public float LowHpFlash = 4f;
        public bool EnableLowHpAudio = true;
        public string LowHpSound = "Bell";                 // Bell / Heartbeat / Mana Chirp / Click / Bell (No Pitch)
        public int LowHpSdFreq = 35;

        // ---------------- 灰视 ----------------
        // 原版靠 shader；这里用暗色叠层近似。
        public bool EnableGrayVision = true;
        public float GrayTrigger = 0.25f;
        public float GrayMaxIntensity = 1f;

        // ---------------- 音乐闷音 ----------------
        public bool EnableAudioMuffle = true;
        public float MuffleTrigger = 0.25f;
        public float MuffleIntensity = 0.3f;

        // ---------------- 血条 ----------------
        public bool EnableHPBar = true;
        public bool HPBarAboveHead = true;      // true = 头顶；false = 脚下（原模组默认位置）
        public float HPBarOffset = 12f;         // 血条离头顶/脚下的像素距离
        public int HPBarDelay = 90;
        public float HPBarTrigger = 1f;
        public float HPBarOpacity = 1f;
        public float HPBarScale = 1f;

        // ---------------- 药水 ----------------
        public bool EnablePSAudio = true;
        public bool EnablePSVisual = true;
        public int PotionDelay = 60;
        public byte PotionOpacity = 255;
        public float PotionScale = 1f;

        // ---------------- Debuff ----------------
        public bool EnableBuffVisual = true;
        public string BuffLayout = "Vertical";             // Vertical / Horizontal / Most recent only
        public bool EnableBuffTimer = false;
        public int BuffDelay = 60;
        public byte BuffOpacity = 255;
        public float BuffScale = 1f;

        public List<string> DebuffBL = new List<string>
        {
            "Campfire", "PeaceCandle", "HeartLamp", "CatBast",
            "StarInBottle", "PotionSickness", "ManaSickness",
            "Sunflower", "MonsterBanner", "Werewolf", "Merfolk"
        };

        [JsonIgnore]
        public bool[] DoNotShow = new bool[BuffID.Count];

        // ------------------------------------------------------------------

        private static string PathFile
        {
            get
            {
                string dir = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "Mods", "HPAware");
                System.IO.Directory.CreateDirectory(dir);
                return System.IO.Path.Combine(dir, "config.json");
            }
        }

        public static PluginConfig Instance { get; set; } = new PluginConfig();

        public static bool LoadedFromFile { get; private set; }

        public static void ResetToDefault()
        {
            Instance = new PluginConfig();
            Instance.RebuildBlacklist();
        }

        public static void Load()
        {
            try
            {
                string path = PathFile;
                LoadedFromFile = System.IO.File.Exists(path);

                if (LoadedFromFile)
                {
                    string json = System.IO.File.ReadAllText(path);
                    PluginConfig loaded = JsonConvert.DeserializeObject<PluginConfig>(json);
                    if (loaded != null)
                        Instance = loaded;
                }
                else
                {
                    Save();
                }

                Instance.RebuildBlacklist();
                Log.Write("HPAware 配置已加载: " + path);
            }
            catch (Exception e)
            {
                Log.Write("HPAware 配置加载失败，使用默认值: " + e.Message);
                Instance = new PluginConfig();
                Instance.RebuildBlacklist();
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Instance, Formatting.Indented);
                System.IO.File.WriteAllText(PathFile, json);
            }
            catch (Exception e)
            {
                Log.Write("HPAware 配置保存失败: " + e.Message);
            }
        }

        public void RebuildBlacklist()
        {
            DoNotShow = new bool[BuffID.Count];

            if (DebuffBL == null)
                return;

            foreach (string name in DebuffBL)
            {
                try
                {
                    int id = BuffID.Search.GetId(name);
                    if (id >= 0 && id < DoNotShow.Length)
                        DoNotShow[id] = true;
                }
                catch
                {
                }
            }
        }

        // ------------------------------------------------------------------

        public Color GetHurtColor()
        {
            Color c = ToColor(HurtColor, new Color(255, 0, 0));
            if (HurtUseDarkColors)
                c = new Color(255 - c.R, 255 - c.G, 255 - c.B);
            return c;
        }

        public Color GetLowHpColor()
        {
            Color c = ToColor(LowHpColor, new Color(255, 0, 0));
            if (LowHpUseDarkColors)
                c = new Color(255 - c.R, 255 - c.G, 255 - c.B);
            return c;
        }

        public static Color ToColor(int[] rgb, Color fallback)
        {
            if (rgb == null || rgb.Length < 3)
                return fallback;
            return new Color(
                (byte)MathHelper.Clamp((float)rgb[0], 0f, 255f),
                (byte)MathHelper.Clamp((float)rgb[1], 0f, 255f),
                (byte)MathHelper.Clamp((float)rgb[2], 0f, 255f));
        }
    }
}
