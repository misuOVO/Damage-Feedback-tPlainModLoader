using System;
using System.Collections.Generic;
using Terraria.UI;
using tContentPatch;
using tContentPatch.Content.UI;
using tContentPatch.Content.UI.ModSet;

namespace HPAware
{
    /// <summary>
    /// 可折叠分组：点标题展开/收起。
    /// </summary>
    public class CollapsibleGroup : UIStackPanel
    {
        private readonly string _title;
        private readonly List<UIElement> _items = new List<UIElement>();
        private readonly UIStackPanel _body = new UIStackPanel();
        private bool _open;

        public CollapsibleGroup(string title, bool defaultOpen)
        {
            _title = title;
            _open = defaultOpen;

            Width.Precent = 1f;
            ItemMargin = 2f;
            IsAutoUpdateSize = true;

            _body.Width.Precent = 1f;
            _body.IsAutoUpdateSize = true;

            Rebuild();
        }

        public void Add(UIElement item)
        {
            _items.Add(item);
            if (_open)
                _body.Append(item);
        }

        private void Toggle()
        {
            _open = !_open;
            Rebuild();
        }

        private void Rebuild()
        {
            RemoveAllChildren();

            UIItemTitle header = new UIItemTitle(null, (_open ? "[-] " : "[+] ") + _title);
            header.OnLeftClick += (evt, el) => Toggle();
            Append(header);

            _body.RemoveAllChildren();
            if (_open)
            {
                foreach (UIElement item in _items)
                    _body.Append(item);
            }
            Append(_body);
        }
    }

    /// <summary>
    /// 让 tPlainModLoader 的“模组设置”页面能显示并修改 HPAware 的配置。
    /// 加载器会自动扫描程序集里所有 ModSetting 子类。
    /// </summary>
    public class HPAwareSetting : ModSetting
    {
        public override string Name => "HPAware";
        public override string Title => "受伤反馈";
        public override string FilePath => "config.json";
        public override bool HasUI => true;
        public override Type DataType => typeof(PluginConfig);

        private static PluginConfig M => PluginConfig.Instance;

        public override void SetDefault()
        {
            PluginConfig.ResetToDefault();
        }

        public override void Load(object v)
        {
            if (v is PluginConfig cfg)
            {
                PluginConfig.Instance = cfg;
                PluginConfig.Instance.RebuildBlacklist();
            }
        }

        public override object GetSaveData()
        {
            return M;
        }

        // ------------------------------------------------------------------

        private void Commit()
        {
            M.RebuildBlacklist();
            NeedSave = true;
            Save();
        }

        private UIItemSwitch Switch(string label, Func<bool> get, Action<bool> set)
        {
            UIItemSwitch item = new UIItemSwitch(null, label);
            item.SetVal(get());
            item.OnValUpdate = v =>
            {
                set(v);
                Commit();
            };
            return item;
        }

        private UIItemValueSlider Slider(string label, float min, float max, Func<float> get, Action<float> set, bool isInt = false)
        {
            UIItemValueSlider item = isInt
                ? new UIItemValueSlider((int)min, (int)max, null, label)
                : new UIItemValueSlider(min, max, null, label);

            // 显示只保留 1 位小数 / 整数，避免 0.4482759 这种数字
            item.FloatToString = v => isInt ? v.ToString("0") : v.ToString("0.0");

            item.SetVal(get());
            item.OnValUpdate = v =>
            {
                set(isInt ? (float)Math.Round(v) : (float)Math.Round(v, 1));
                Commit();
            };
            return item;
        }

        public override UIElement GetUI()
        {
            // 用加载器自带的滚动控件：右上角有滚动条，内容不会超出面板
            UIScrollViewer2 root = new UIScrollViewer2();
            root.Width.Precent = 1f;
            root.Height.Precent = 1f;
            root.ItemMargin = 4;

            // ---- 受伤（默认展开） ----
            CollapsibleGroup hurt = new CollapsibleGroup("受伤", true);
            hurt.Add(Switch("受伤红屏", () => M.EnableHurtOverlay, v => M.EnableHurtOverlay = v));
            hurt.Add(Slider("红屏强度", 0.1f, 2f, () => M.HurtAlpha, v => M.HurtAlpha = v));
            hurt.Add(Slider("红屏淡出速度", 1f, 10f, () => M.HurtSpeed, v => M.HurtSpeed = (int)v, true));
            root.AddChild(hurt);

            // ---- 红框外观（默认展开） ----
            CollapsibleGroup look = new CollapsibleGroup("红框外观", true);
            look.Add(Slider("圆润度", 0f, 1f, () => M.OverlayRoundness, v => M.OverlayRoundness = v));
            look.Add(Slider("渐变范围", 0.1f, 1f, () => M.OverlayThickness, v => M.OverlayThickness = v));
            look.Add(Slider("闪烁强度", 0f, 1f, () => M.OverlayFlicker, v => M.OverlayFlicker = v));
            root.AddChild(look);

            // ---- 头顶血条（默认展开） ----
            CollapsibleGroup bar = new CollapsibleGroup("头顶血条", true);
            bar.Add(Switch("显示血条", () => M.EnableHPBar, v => M.EnableHPBar = v));
            bar.Add(Switch("血条在头顶", () => M.HPBarAboveHead, v => M.HPBarAboveHead = v));
            bar.Add(Slider("显示时长(帧)", 10f, 610f, () => M.HPBarDelay, v => M.HPBarDelay = (int)v, true));
            bar.Add(Slider("血条缩放", 0.3f, 2f, () => M.HPBarScale, v => M.HPBarScale = v));
            bar.Add(Slider("血条不透明度", 0.1f, 1f, () => M.HPBarOpacity, v => M.HPBarOpacity = v));
            root.AddChild(bar);

            // ---- 低血量（默认收起） ----
            CollapsibleGroup low = new CollapsibleGroup("低血量", false);
            low.Add(Switch("低血量脉动", () => M.EnableLowHpOverlay, v => M.EnableLowHpOverlay = v));
            low.Add(Slider("触发比例", 0.1f, 1f, () => M.Overlaytrigger, v => M.Overlaytrigger = v));
            low.Add(Slider("脉动强度", 0.1f, 1f, () => M.LowHpAlpha, v => M.LowHpAlpha = v));
            low.Add(Switch("低血量音效", () => M.EnableLowHpAudio, v => M.EnableLowHpAudio = v));
            low.Add(Slider("音效间隔(帧)", 1f, 180f, () => M.LowHpSdFreq, v => M.LowHpSdFreq = (int)v, true));
            root.AddChild(low);

            // ---- 药水（默认收起） ----
            CollapsibleGroup potion = new CollapsibleGroup("药水冷却", false);
            potion.Add(Switch("冷却结束显示图标", () => M.EnablePSVisual, v => M.EnablePSVisual = v));
            potion.Add(Switch("冷却结束播放音效", () => M.EnablePSAudio, v => M.EnablePSAudio = v));
            potion.Add(Slider("图标时长(帧)", 10f, 180f, () => M.PotionDelay, v => M.PotionDelay = (int)v, true));
            potion.Add(Slider("图标缩放", 0.3f, 2f, () => M.PotionScale, v => M.PotionScale = v));
            root.AddChild(potion);

            // ---- Debuff（默认收起） ----
            CollapsibleGroup debuff = new CollapsibleGroup("Debuff 提示", false);
            debuff.Add(Switch("显示 debuff 图标", () => M.EnableBuffVisual, v => M.EnableBuffVisual = v));
            debuff.Add(Switch("显示剩余时间", () => M.EnableBuffTimer, v => M.EnableBuffTimer = v));
            debuff.Add(Slider("图标时长(帧)", 10f, 610f, () => M.BuffDelay, v => M.BuffDelay = (int)v, true));
            debuff.Add(Slider("图标缩放", 0.3f, 2f, () => M.BuffScale, v => M.BuffScale = v));
            root.AddChild(debuff);

            // ---- 其它（默认收起） ----
            CollapsibleGroup misc = new CollapsibleGroup("其它", false);
            misc.Add(Switch("低血量音乐闷音", () => M.EnableAudioMuffle, v => M.EnableAudioMuffle = v));
            misc.Add(Slider("闷音强度", 0f, 1f, () => M.MuffleIntensity, v => M.MuffleIntensity = v));
            misc.Add(Switch("灰视(近似)", () => M.EnableGrayVision, v => M.EnableGrayVision = v));
            root.AddChild(misc);

            return root;
        }
    }
}
