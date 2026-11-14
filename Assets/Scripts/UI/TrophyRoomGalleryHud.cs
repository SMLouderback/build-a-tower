using UnityEngine;

namespace BuildATower
{
    /// <summary>
    /// Close-up Trophy Room gallery. Draws a large interior plate and one readable
    /// pedestal per subject. Empty slots stay a muted silhouette; filled slots use
    /// the shared <see cref="TrophyRoomDisplay"/> metal sprite.
    /// </summary>
    public static class TrophyRoomGalleryHud
    {
        public const string CloseupResourcePath = "Art/Dollhouse/trophy_room_gallery_closeup";
        public const string ShellResourcePath = "Art/Dollhouse/trophy_room_6x1";
        public const string EmptyMarker = "Empty";
        public const float PreferredPanelWidth = 960f;
        public const float PreferredPanelHeight = 560f;
        public const int Columns = 5;

        static Texture2D _background;
        static bool _backgroundLoaded;
        static Texture2D _white;
        static Texture2D _silhouette;

        public static string ResolveBackgroundPath(bool closeupPresent) =>
            closeupPresent ? CloseupResourcePath : ShellResourcePath;

        public static string FormatPedestalLabel(TrophyRoomDisplay.PedestalSlot slot)
        {
            if (!slot.HasTrophy)
                return slot.Subject + "\n" + EmptyMarker;
            return slot.Subject + "\n" + slot.Metal;
        }

        public static string[] FormatPedestalLabels(TrophyCase trophyCase)
        {
            var slots = TrophyRoomDisplay.BuildPedestals(trophyCase);
            var labels = new string[slots.Count];
            for (var i = 0; i < slots.Count; i++)
                labels[i] = FormatPedestalLabel(slots[i]);
            return labels;
        }

        public static string PedestalSpritePath(TrophyRoomDisplay.PedestalSlot slot) =>
            slot.HasTrophy
                ? TrophyRoomDisplay.MetalSpriteResourcePath(slot.SpriteKey)
                : string.Empty;

        /// <summary>
        /// Lower band of the panel, under the enlarged wall and frames.
        /// </summary>
        public static Rect PedestalContentRect(Rect panel)
        {
            var top = panel.y + Mathf.Max(52f, panel.height * 0.42f);
            var height = Mathf.Max(80f, panel.yMax - 12f - top);
            return new Rect(panel.x + 12f, top, Mathf.Max(40f, panel.width - 24f), height);
        }

        public static Rect[] LayoutPedestalCells(Rect content, int count)
        {
            if (count <= 0)
                return System.Array.Empty<Rect>();

            var cols = count < Columns ? count : Columns;
            var rows = (count + cols - 1) / cols;
            var cells = new Rect[count];
            var cellW = content.width / cols;
            var cellH = content.height / rows;
            for (var i = 0; i < count; i++)
            {
                var col = i % cols;
                var row = i / cols;
                cells[i] = new Rect(
                    content.x + col * cellW,
                    content.y + row * cellH,
                    cellW,
                    cellH);
            }

            return cells;
        }

        /// <summary>
        /// Draws the gallery inside <paramref name="panel"/>. Returns true when Close is clicked.
        /// Does not touch pause or speed.
        /// </summary>
        public static bool Draw(Rect panel, TrophyCase trophyCase, GUIStyle title, GUIStyle label)
        {
            GUI.Box(panel, GUIContent.none);
            DrawBackground(panel);
            DrawTitleScrim(panel);

            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 14f, panel.width - 140f, 28f),
                "Trophy Room",
                title);
            var close = GUI.Button(new Rect(panel.xMax - 108f, panel.y + 14f, 88f, 28f), "Close");

            var content = PedestalContentRect(panel);
            var slots = TrophyRoomDisplay.BuildPedestals(trophyCase);
            var cells = LayoutPedestalCells(content, slots.Count);
            var n = slots.Count < cells.Length ? slots.Count : cells.Length;
            for (var i = 0; i < n; i++)
                DrawPedestal(cells[i], slots[i], label);

            return close;
        }

        static void DrawBackground(Rect panel)
        {
            var tex = Background();
            if (tex == null)
                return;

            var inner = new Rect(panel.x + 4f, panel.y + 4f, panel.width - 8f, panel.height - 8f);
            GUI.DrawTexture(inner, tex, ScaleMode.ScaleAndCrop, true);
        }

        static void DrawTitleScrim(Rect panel)
        {
            var prev = GUI.color;
            GUI.color = new Color(0.14f, 0.10f, 0.08f, 0.78f);
            GUI.DrawTexture(new Rect(panel.x + 4f, panel.y + 4f, panel.width - 8f, 44f), White());
            GUI.color = prev;
        }

        static void DrawPedestal(Rect cell, TrophyRoomDisplay.PedestalSlot slot, GUIStyle label)
        {
            var iconSize = Mathf.Min(cell.width - 16f, cell.height * 0.62f);
            var icon = new Rect(
                cell.x + (cell.width - iconSize) * 0.5f,
                cell.y + 6f,
                iconSize,
                iconSize);

            if (slot.HasTrophy)
                DrawMedal(icon, slot.SpriteKey);
            else
                DrawEmptySilhouette(icon);

            var caption = new Rect(cell.x + 4f, icon.yMax + 2f, cell.width - 8f, Mathf.Max(32f, cell.yMax - icon.yMax - 6f));
            var prev = GUI.color;
            GUI.color = new Color(0.10f, 0.08f, 0.06f, slot.HasTrophy ? 0.62f : 0.42f);
            GUI.DrawTexture(caption, White());
            GUI.color = prev;

            var hadStyle = label != null;
            var prevColor = hadStyle ? label.normal.textColor : Color.white;
            var prevAlign = hadStyle ? label.alignment : TextAnchor.UpperLeft;
            if (hadStyle)
            {
                label.alignment = TextAnchor.UpperCenter;
                if (!slot.HasTrophy)
                    label.normal.textColor = new Color(0.62f, 0.56f, 0.48f, 1f);
            }

            GUI.Label(caption, FormatPedestalLabel(slot), label);

            if (hadStyle)
            {
                label.normal.textColor = prevColor;
                label.alignment = prevAlign;
            }
        }

        static void DrawMedal(Rect icon, string spriteKey)
        {
            var sprite = TrophyRoomDisplay.LoadChipSprite(spriteKey);
            if (sprite != null && sprite.texture != null)
            {
                GUI.DrawTexture(icon, sprite.texture, ScaleMode.ScaleToFit, true);
                return;
            }

            DrawEmptySilhouette(icon);
        }

        static void DrawEmptySilhouette(Rect icon)
        {
            var prev = GUI.color;
            GUI.color = new Color(0.45f, 0.40f, 0.35f, 0.58f);
            GUI.DrawTexture(icon, Silhouette(), ScaleMode.ScaleToFit, true);
            GUI.color = prev;
        }

        static Texture2D Background()
        {
            if (_backgroundLoaded)
                return _background;

            _backgroundLoaded = true;
            _background = LoadPlate(ResolveBackgroundPath(true))
                          ?? LoadPlate(ResolveBackgroundPath(false));
            return _background;
        }

        static Texture2D LoadPlate(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            Texture2D source = null;
            var owned = false;
            var bytesAsset = Resources.Load<TextAsset>(path);
            var png = bytesAsset != null ? bytesAsset.bytes : null;
            if (png != null && png.Length >= 32)
            {
                source = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = path,
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!source.LoadImage(png, false))
                {
                    DestroyOwned(source);
                    return null;
                }

                owned = true;
            }
            else
            {
                source = Resources.Load<Texture2D>(path);
                if (source == null)
                    return null;
            }

            var keyed = RoomDollhouseArt.KeyMagentaKeepCanvas(source, owned);
            if (keyed != null)
                keyed.hideFlags = HideFlags.HideAndDontSave;
            return keyed;
        }

        static Texture2D White()
        {
            if (_white != null)
                return _white;

            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            _white.SetPixel(0, 0, Color.white);
            _white.Apply(false, true);
            return _white;
        }

        static Texture2D Silhouette()
        {
            if (_silhouette != null)
                return _silhouette;

            const int s = 64;
            var px = new Color[s * s];
            var ink = new Color(0.82f, 0.78f, 0.72f, 1f);

            void Fill(int x0, int y0, int x1, int y1)
            {
                for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                {
                    if ((uint)x >= s || (uint)y >= s)
                        continue;
                    px[y * s + x] = ink;
                }
            }

            Fill(12, 4, 51, 10);
            Fill(18, 11, 45, 18);
            Fill(27, 19, 36, 30);
            Fill(18, 31, 45, 52);
            Fill(12, 36, 17, 48);
            Fill(46, 36, 51, 48);
            Fill(22, 46, 41, 50);

            _silhouette = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _silhouette.SetPixels(px);
            _silhouette.Apply(false, true);
            return _silhouette;
        }

        static void DestroyOwned(Object obj)
        {
            if (obj == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }
    }
}
