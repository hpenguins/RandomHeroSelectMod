using System;
using CesiumLoader.SDK.Logging;
using CesiumLoader.SDK.UserInterface;
using UnityEngine;

namespace RandomHeroSelectMod
{
    /// <summary>
    /// 选人阶段显示在屏幕最右侧、垂直居中的「随机选人」按钮。
    ///
    /// 用 SDK 的 WidgetService 反射建一个独立 uGUI Canvas（不碰游戏的 FairyGUI），
    /// 因此不需要依赖任何游戏 UI 类型；Canvas 跨场景保留，场景把它带走时下次显示会重建。
    /// 只在可以选角色且选人面板存在时显示，其余时候整个按钮隐藏（不接收点击）。
    /// </summary>
    internal sealed class RandomSelectButton : IDisposable
    {
        private const string CanvasName = "RandomHeroSelectCanvas";
        private const string ButtonText = "随机选人";
        private const int SortingOrder = 2000;

        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 54f;

        /// <summary>贴边锚点时离屏幕边缘的默认距离（CanvasScaler 参考分辨率 1920×1080 下的像素）。</summary>
        private const float EdgeInset = 48f;

        private static readonly Color Background = new Color(0.30f, 0.21f, 0.52f, 0.95f);

        private readonly ModLogger _log;
        private readonly Action _onClick;

        private WidgetScope _scope;
        private WidgetHandle _canvas;
        private WidgetHandle _button;
        private WidgetHandle _label;

        private WidgetAnchor _anchor = WidgetAnchor.MiddleRight;
        private float _offsetX;
        private float _offsetY;

        private bool _disposed;
        private bool _visible;
        private bool _warnedUnavailable;

        internal RandomSelectButton(ModLogger log, Action onClick)
        {
            _log = log;
            _onClick = onClick;
        }

        /// <summary>
        /// 调整按钮位置。锚点决定贴哪条边，锚点到边距固定 48px，
        /// offset 是额外的像素偏移（正数向右 / 向上）。控件已建好时立即移动。
        /// </summary>
        internal void SetPlacement(WidgetAnchor anchor, float offsetX, float offsetY)
        {
            if (_disposed) return;

            bool changed = _anchor != anchor || _offsetX != offsetX || _offsetY != offsetY;
            _anchor = anchor;
            _offsetX = offsetX;
            _offsetY = offsetY;
            if (!changed) return;

            if (_button != null && _button.IsAlive)
            {
                WidgetService.Place(_button.Rect, _anchor, ResolvePosition(_anchor, _offsetX, _offsetY),
                    new Vector2(ButtonWidth, ButtonHeight));
            }
        }

        /// <summary>把「锚点 + 额外偏移」换算成 WidgetService.Place 需要的 anchoredPosition。</summary>
        private static Vector2 ResolvePosition(WidgetAnchor anchor, float offsetX, float offsetY)
        {
            float x = offsetX;
            float y = offsetY;

            switch (anchor)
            {
                case WidgetAnchor.TopLeft:
                case WidgetAnchor.MiddleLeft:
                case WidgetAnchor.BottomLeft:
                    x += EdgeInset;
                    break;
                case WidgetAnchor.TopRight:
                case WidgetAnchor.MiddleRight:
                case WidgetAnchor.BottomRight:
                    x -= EdgeInset;
                    break;
            }

            switch (anchor)
            {
                case WidgetAnchor.TopLeft:
                case WidgetAnchor.Top:
                case WidgetAnchor.TopRight:
                    y -= EdgeInset;
                    break;
                case WidgetAnchor.BottomLeft:
                case WidgetAnchor.Bottom:
                case WidgetAnchor.BottomRight:
                    y += EdgeInset;
                    break;
            }

            return new Vector2(x, y);
        }

        /// <summary>显示 / 隐藏按钮；第一次显示时才真正创建 Canvas 与控件。</summary>
        internal void SetVisible(bool visible)
        {
            if (_disposed) return;

            if (!visible)
            {
                if (_button != null && _button.IsAlive) _button.SetActive(false);
                _visible = false;
                return;
            }

            if (!EnsureBuilt()) return;

            if (!_visible)
            {
                _button.SetActive(true);
                _visible = true;
            }
        }

        /// <summary>确认控件是否已经建好；场景切换销毁 Canvas 后自动重建一次。</summary>
        private bool EnsureBuilt()
        {
            if (_button != null && _button.IsAlive) return true;
            if (_button != null) ResetBuild();

            if (!WidgetService.IsAvailable)
            {
                if (!_warnedUnavailable)
                {
                    _warnedUnavailable = true;
                    _log.Warn("uGUI 不可用，随机选人按钮无法显示：" + WidgetService.UnavailableReason);
                }
                return false;
            }

            _scope = WidgetService.CreateScope("random-hero-select");

            _canvas = _scope.Track(WidgetService.Canvas(CanvasName, SortingOrder, persist: true));
            if (_canvas == null)
            {
                _log.Warn("随机选人按钮创建失败：Canvas 创建失败");
                ResetBuild();
                return false;
            }

            // 按钮需要 EventSystem；游戏里已有就直接复用。
            WidgetService.EnsureEventSystem();

            _button = WidgetService.Button(_canvas.Rect, Background, rounded: true);
            if (_button == null)
            {
                _log.Warn("随机选人按钮创建失败：uGUI Button 不可用");
                ResetBuild();
                return false;
            }
            WidgetService.Place(_button.Rect, _anchor, ResolvePosition(_anchor, _offsetX, _offsetY),
                new Vector2(ButtonWidth, ButtonHeight));
            _scope.Track(_button);

            _label = WidgetService.Text(_button.Rect, ButtonText, 18, Color.white, WidgetAnchor.Center);
            if (_label != null)
            {
                WidgetService.Stretch(_label.Rect);
                _scope.Track(_label);
            }

            if (!WidgetService.SetOnClick(_button.Rect, HandleClick))
            {
                _log.Warn("随机选人按钮创建失败：点击事件绑定失败");
                ResetBuild();
                return false;
            }

            _visible = false;
            _button.SetActive(false);
            _log.Info("选人按钮已创建（屏幕最右侧居中）");
            return true;
        }

        private void HandleClick()
        {
            if (_disposed) return;
            try
            {
                if (_onClick != null) _onClick();
            }
            catch (Exception e)
            {
                _log.Warn("随机选人按钮回调异常: " + e.Message);
            }
        }

        /// <summary>丢弃当前控件（Canvas 已失效或构建中途失败）。</summary>
        private void ResetBuild()
        {
            try { if (_scope != null) _scope.Dispose(); } catch { }
            _scope = null;
            _canvas = null;
            _button = null;
            _label = null;
            _visible = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ResetBuild();
        }
    }
}
