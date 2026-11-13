using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace HPAware
{
    /// <summary>
    /// 绘制层：屏幕叠层（受伤/低血/灰视）+ 头顶 UI（血条/药水/debuff）。
    ///
    /// 红框不用 shader：运行时生成一张“圆角渐晕”贴图（边缘不透明、向内渐隐），
    /// 按屏幕尺寸拉伸，再用颜色/透明度控制闪烁和血量强度。
    /// </summary>
    internal static class AwareDraw
    {
        private static Texture2D _potionTexture;

        private static Texture2D _vignette;
        private static int _vigW;
        private static int _vigH;
        private static float _vigRound = -1f;
        private static float _vigThick = -1f;

        private static Texture2D PotionTexture
        {
            get
            {
                if (_potionTexture == null)
                {
                    Assembly asm = Assembly.GetExecutingAssembly();
                    using (Stream s = asm.GetManifestResourceStream("HPAware.Assets.PotionReady.png"))
                    {
                        if (s == null)
                            return null;

                        using (MemoryStream ms = new MemoryStream())
                        {
                            s.CopyTo(ms);
                            ms.Position = 0;
                            _potionTexture = Texture2D.FromStream(Main.instance.GraphicsDevice, ms);
                        }
                    }
                }
                return _potionTexture;
            }
        }

        public static void DrawAll(SpriteBatch sb)
        {
            if (sb == null || Main.dedServ || Main.gameMenu)
                return;

            Player p = Main.LocalPlayer;
            if (p == null || !p.active || p.dead || p.ghost)
                return;

            PluginConfig M = PluginConfig.Instance;

            // 屏幕叠层用 UI 坐标（UIScaleMatrix）
            DrawScreenOverlays(sb, p, M);

            // 头顶 UI 必须用世界矩阵（ZoomMatrix），否则只减 screenPosition
            // 在缩放/UI 比例下会偏到屏幕别的地方
            DrawWorldWidgets(sb, p, M);
        }

        private static void DrawWorldWidgets(SpriteBatch sb, Player p, PluginConfig M)
        {
            bool switched = false;

            try
            {
                sb.End();
                sb.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    SamplerState.LinearClamp,
                    DepthStencilState.None,
                    RasterizerState.CullCounterClockwise,
                    null,
                    Main.GameViewMatrix.ZoomMatrix);

                switched = true;

                DrawDebuffs(sb, p, M);
                DrawPotion(sb, p, M);
                DrawHpBar(sb, p, M);
            }
            catch (Exception e)
            {
                Log.Write("HPAware 世界层绘制失败: " + e.Message);
            }
            finally
            {
                if (switched)
                {
                    sb.End();
                    sb.Begin(
                        SpriteSortMode.Deferred,
                        BlendState.AlphaBlend,
                        SamplerState.LinearClamp,
                        DepthStencilState.None,
                        RasterizerState.CullCounterClockwise,
                        null,
                        Main.UIScaleMatrix);
                }
            }
        }

        // ------------------------------------------------------------------
        // 屏幕叠层
        // ------------------------------------------------------------------

        private static void DrawScreenOverlays(SpriteBatch sb, Player p, PluginConfig M)
        {
            float hpRatio = p.statLifeMax2 > 0 ? (float)p.statLife / p.statLifeMax2 : 1f;

            // 受伤：被打了就亮一下，然后淡出
            float hurt = 0f;
            Color hurtColor = M.GetHurtColor();
            if (M.EnableHurtOverlay && AwareState.ShaderFade > 0f)
                hurt = M.HurtAlpha * AwareState.ShaderFade;

            // 低血量：血越少越红，并持续“忽闪”
            float low = 0f;
            Color lowColor = M.GetLowHpColor();
            if (M.EnableLowHpOverlay && hpRatio <= M.Overlaytrigger && M.Overlaytrigger > 0f)
            {
                float redness = MathHelper.Clamp((M.Overlaytrigger - hpRatio) / M.Overlaytrigger, 0f, 1f);
                low = M.LowHpAlpha * (0.35f + 0.65f * redness) * Flicker(M);
            }

            if (hurt <= 0f && low <= 0f)
                return;

            // HurtOverlayFlat：整屏均匀红（原模组的另一种模式）
            if (M.HurtOverlayType == "HPOverlayFlat" && hurt > 0f)
            {
                Color flatColor = hurtColor * MathHelper.Clamp(hurt, 0f, 1f);
                sb.Draw(TextureAssets.MagicPixel.Value,
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), flatColor);
                hurt = 0f;
            }

            if (low >= hurt)
            {
                Color c = lowColor * MathHelper.Clamp(low, 0f, 1f);
                DrawVignette(sb, M, c);
            }
            else
            {
                Color c = hurtColor * MathHelper.Clamp(hurt, 0f, 1f);
                DrawVignette(sb, M, c);
            }

            DrawGrayVision(sb, p, M);
        }

        /// <summary>忽闪：平滑正弦 + 轻微抖动，避免机械感。</summary>
        private static float Flicker(PluginConfig M)
        {
            if (M.OverlayFlicker <= 0f)
                return 1f;

            float t = Main.GlobalTimeWrappedHourly;
            float slow = (float)Math.Sin(t * 6.4f) * 0.5f + 0.5f;
            float fast = (float)Math.Sin(t * 14.7f + 1.7f) * 0.5f + 0.5f;
            float pulse = MathHelper.Clamp(slow * 0.7f + fast * 0.3f, 0f, 1f);

            // 0.55 ~ 1.0 之间脉动，强度由配置决定
            return 1f - M.OverlayFlicker * 0.45f * (1f - pulse);
        }

        private static void DrawGrayVision(SpriteBatch sb, Player p, PluginConfig M)
        {
            if (!M.EnableGrayVision)
                return;
            if (p.statLife > p.statLifeMax2 * M.GrayTrigger)
                return;

            float hpPercent = (float)p.statLife / ((float)p.statLifeMax2 * M.GrayTrigger);
            float intensity = MathHelper.Clamp((1f - hpPercent) * M.GrayMaxIntensity, 0f, M.GrayMaxIntensity);

            byte a = (byte)(120 * intensity);
            sb.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                new Color((byte)40, (byte)40, (byte)40, a));
        }

        private static void DrawVignette(SpriteBatch sb, PluginConfig M, Color color)
        {
            if (color.A == 0 && color.R == 0 && color.G == 0 && color.B == 0)
                return;

            Texture2D tex = GetVignette(M);
            if (tex == null)
                return;

            sb.Draw(tex, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), color);
        }

        /// <summary>
        /// 生成圆角渐晕贴图：边缘 alpha=1，向内按 roundness/thickness 渐隐。
        /// 贴图是预乘 alpha 的白色，绘制时乘上颜色即可。
        /// </summary>
        private static Texture2D GetVignette(PluginConfig M)
        {
            int w = Main.screenWidth;
            int h = Main.screenHeight;
            if (w <= 0 || h <= 0)
                return null;

            float round = MathHelper.Clamp(M.OverlayRoundness, 0f, 1f);
            float thick = MathHelper.Clamp(M.OverlayThickness, 0.05f, 1f);

            if (_vignette != null && !_vignette.IsDisposed
                && _vigW == w && _vigH == h
                && Math.Abs(_vigRound - round) < 0.001f
                && Math.Abs(_vigThick - thick) < 0.001f)
            {
                return _vignette;
            }

            try
            {
                // 太大的贴图浪费显存，按最长边 512 采样后再放大
                int scale = Math.Max(1, (int)Math.Ceiling(Math.Max(w, h) / 512f));
                int tw = Math.Max(2, w / scale);
                int th = Math.Max(2, h / scale);

                Color[] data = new Color[tw * th];

                float radius = Math.Min(tw, th) * 0.5f * round;
                float maxDepth = Math.Min(tw, th) * thick;

                for (int y = 0; y < th; y++)
                {
                    for (int x = 0; x < tw; x++)
                    {
                        float px = x + 0.5f;
                        float py = y + 0.5f;

                        // 圆角矩形 SDF
                        float dx = Math.Abs(px - tw * 0.5f) - (tw * 0.5f - radius);
                        float dy = Math.Abs(py - th * 0.5f) - (th * 0.5f - radius);

                        float outsideX = Math.Max(dx, 0f);
                        float outsideY = Math.Max(dy, 0f);
                        float outside = (float)Math.Sqrt(outsideX * outsideX + outsideY * outsideY);
                        float inside = Math.Min(Math.Max(dx, dy), 0f);
                        float sdf = outside + inside - radius;

                        float depth = -sdf;                        // 离边缘的“深入”距离
                        float t = MathHelper.Clamp(depth / maxDepth, 0f, 1f);
                        float a = (float)Math.Pow(1f - t, 1.7f);   // 边缘最强，向内平滑变淡

                        byte v = (byte)(MathHelper.Clamp(a, 0f, 1f) * 255f);
                        data[y * tw + x] = new Color(v, v, v, v);  // 预乘 alpha 的白色
                    }
                }

                if (_vignette != null && !_vignette.IsDisposed)
                    _vignette.Dispose();

                Texture2D tex = new Texture2D(Main.instance.GraphicsDevice, tw, th, false, SurfaceFormat.Color);
                tex.SetData(data);

                _vignette = tex;
                _vigW = w;
                _vigH = h;
                _vigRound = round;
                _vigThick = thick;

                return _vignette;
            }
            catch (Exception e)
            {
                Log.Write("HPAware 渐晕贴图生成失败: " + e.Message);
                return null;
            }
        }

        // ------------------------------------------------------------------
        // 头顶 UI
        // ------------------------------------------------------------------

        private static void DrawHpBar(SpriteBatch sb, Player p, PluginConfig M)
        {
            if (!M.EnableHPBar || AwareState.BarAlphaLerp <= 0f)
                return;

            float hpPercent = (float)p.statLife / (float)p.statLifeMax2;
            if (hpPercent > 1f)
                hpPercent = 1f;

            int hp36 = (int)(36f * hpPercent);
            float scale = M.HPBarScale;

            float x = p.Center.X - 18f * scale;
            float y = M.HPBarAboveHead
                ? p.position.Y - M.HPBarOffset * scale
                : p.Center.Y + p.height - 10f;

            if (p.gravDir != 1f)
            {
                y = 2 * Main.screenPosition.Y + Main.screenHeight - y;
            }

            float r;
            float g;
            float b = 0f;
            float a = 255f;

            hpPercent -= 0.1f;
            if (hpPercent > 0.5f)
            {
                g = 255f;
                r = 255f * (1f - hpPercent) * 2f;
            }
            else
            {
                g = 255f * hpPercent * 2f;
                r = 255f;
            }

            r = MathHelper.Clamp(r, 0f, 255f);
            g = MathHelper.Clamp(g, 0f, 255f);
            a = MathHelper.Clamp(a, 0f, 255f);

            Color col = new Color((byte)r, (byte)g, (byte)b, (byte)a);
            col *= M.HPBarOpacity * AwareState.BarAlphaLerp;

            if (hp36 < 3)
                hp36 = 3;

            Vector2 pos = new Vector2(x - Main.screenPosition.X, y - Main.screenPosition.Y);

            Texture2D hb1 = TextureAssets.Hb1.Value;
            Texture2D hb2 = TextureAssets.Hb2.Value;

            if (hp36 < 34)
            {
                sb.Draw(hb2, pos + new Vector2(hp36 * scale, 0f),
                    new Rectangle(2, 0, 2, hb2.Height), col, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                sb.Draw(hb2, pos + new Vector2((hp36 + 2) * scale, 0f),
                    new Rectangle(hp36 + 2, 0, 36 - hp36 - 2, hb2.Height), col, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                sb.Draw(hb1, pos,
                    new Rectangle(0, 0, hp36 - 2, hb1.Height), col, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                sb.Draw(hb1, pos + new Vector2((hp36 - 2) * scale, 0f),
                    new Rectangle(32, 0, 2, hb1.Height), col, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            else
            {
                if (hp36 < 36)
                {
                    sb.Draw(hb2, pos + new Vector2(hp36 * scale, 0f),
                        new Rectangle(hp36, 0, 36 - hp36, hb2.Height), col, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }

                sb.Draw(hb1, pos,
                    new Rectangle(0, 0, hp36, hb1.Height), col, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawPotion(SpriteBatch sb, Player p, PluginConfig M)
        {
            if (!M.EnablePSVisual || AwareState.PotionTimer <= 0)
                return;

            Texture2D tex = PotionTexture;
            if (tex == null)
                return;

            Vector2 position = new Vector2(p.Center.X, p.position.Y);
            Vector2 offset = new Vector2(10f, 40f);

            if (AwareState.DebuffTimer > 0)
            {
                if (M.BuffLayout == "Vertical")
                {
                    offset.X = 35f;
                }
                else
                {
                    offset.Y /= M.PotionScale;
                    offset.Y *= M.BuffScale;
                    offset.Y += (!M.EnableBuffTimer || M.BuffLayout == "Most recent only")
                        ? 30f
                        : 30f + 25f * M.BuffScale;
                }
            }

            if (p.gravDir != 1f)
            {
                position.Y = 2 * Main.screenPosition.Y + Main.screenHeight - position.Y;
                offset.Y += 40f / M.PotionScale;
            }

            Vector2 drawPos = position - Main.screenPosition - offset * M.PotionScale;
            Color opacity = new Color(M.PotionOpacity, M.PotionOpacity, M.PotionOpacity, M.PotionOpacity);

            sb.Draw(tex, drawPos, new Rectangle(0, 0, 28, 30), opacity, 0f, Vector2.Zero,
                M.PotionScale, SpriteEffects.None, 1f);
        }

        private static void DrawDebuffs(SpriteBatch sb, Player p, PluginConfig M)
        {
            if (!M.EnableBuffVisual || AwareState.DebuffTimer <= 0)
                return;
            if (AwareState.DebuffsToShow.Count == 0)
                return;

            Vector2 position = new Vector2(p.Center.X, p.position.Y);
            Vector2 offset = new Vector2(15f, 40f);
            Color opacity = new Color(M.BuffOpacity, M.BuffOpacity, M.BuffOpacity, M.BuffOpacity);

            if (AwareState.PotionTimer > 0 && M.BuffLayout == "Vertical")
                offset.X = -5f;

            if (p.gravDir != 1f)
            {
                position.Y = 2 * Main.screenPosition.Y + Main.screenHeight - position.Y;
                offset.Y += 40f / M.BuffScale;
            }

            if (M.BuffLayout != "Most recent only")
            {
                float baseOffsetY = offset.Y;

                for (int i = 0; i < AwareState.DebuffsToShow.Count; i++)
                {
                    if (M.BuffLayout == "Vertical")
                    {
                        offset.Y = 30f * (AwareState.DebuffsToShow.Count - 1);
                        offset.Y += i * -30f;
                        offset.Y += baseOffsetY;
                    }
                    else
                    {
                        offset.X = i * -30f + AwareState.DebuffsToShow.Count * 15f;
                    }

                    Vector2 realPos = position - Main.screenPosition - offset * M.BuffScale;
                    int type = AwareState.DebuffsToShow[i];

                    if (type < 0 || type >= TextureAssets.Buff.Length)
                        continue;

                    sb.Draw(TextureAssets.Buff[type].Value, realPos,
                        new Rectangle(0, 0, 32, 32), opacity, 0f, Vector2.Zero,
                        M.BuffScale, SpriteEffects.None, 1f);

                    if (M.EnableBuffTimer)
                    {
                        Vector2 side = (M.BuffLayout == "Vertical")
                            ? new Vector2(35f, 8f) * M.BuffScale
                            : new Vector2(2f, -14f - i % 2 * 15f) * M.BuffScale;

                        DrawBuffTime(sb, p, type, realPos + side, M);
                    }
                }
            }
            else if (p.FindBuffIndex(AwareState.DebuffToShow) >= 0)
            {
                int type = AwareState.DebuffToShow;
                if (type < 0 || type >= TextureAssets.Buff.Length)
                    return;

                Vector2 realPos = position - Main.screenPosition - offset * M.BuffScale;

                sb.Draw(TextureAssets.Buff[type].Value, realPos,
                    new Rectangle(0, 0, 32, 32), opacity, 0f, Vector2.Zero,
                    M.BuffScale, SpriteEffects.None, 1f);

                if (M.EnableBuffTimer)
                {
                    DrawBuffTime(sb, p, type,
                        realPos + new Vector2(35f * M.BuffScale, 8f * M.BuffScale), M);
                }
            }
        }

        private static void DrawBuffTime(SpriteBatch sb, Player p, int type, Vector2 position, PluginConfig M)
        {
            int index = p.FindBuffIndex(type);
            if (index < 0)
                return;

            if (!Main.TryGetBuffTime(index, out int buffTimeValue) || buffTimeValue <= 2)
                return;

            string text = Lang.LocalizedDuration(new TimeSpan(0, 0, buffTimeValue / 60), true, false);
            Color opacity = new Color(M.BuffOpacity, M.BuffOpacity, M.BuffOpacity, M.BuffOpacity);

            DynamicSpriteFontExtensionMethods.DrawString(sb, FontAssets.MouseText.Value, text, position,
                opacity, 0f, Vector2.Zero, Math.Max(0.1f, M.BuffScale - 0.2f), SpriteEffects.None, 1f);
        }
    }
}
