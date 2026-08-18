#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Editor.FlowIoC.CustomFolderDrawer.Scripts
{
    [InitializeOnLoad]
    public static class CustomFolderDrawer
    {
        private static ED_CustomFolderConfig _config;
        private static Dictionary<string, CFD_ProjectFolderColorRule> _folderRuleMap;
        private const string EDITOR_PATH = "Assets/Editor/FlowIoC/CustomFolderDrawer/Configs/ED_CustomFolderConfig.asset";

        static CustomFolderDrawer()
        {
            Apply();
        }

        public static void Apply()
        {
            _config = AssetDatabase.LoadAssetAtPath<ED_CustomFolderConfig>(EDITOR_PATH);
            _folderRuleMap = new Dictionary<string, CFD_ProjectFolderColorRule>();
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;

            if (!_config.Enabled) return;

            foreach (CFD_ProjectFolderColorRule folderColorRule in _config.FolderRules)
            {
                _folderRuleMap.Add(folderColorRule.FolderRule.Path, folderColorRule);
            }

            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        }

        private static void OnProjectWindowItemGUI(string guid, Rect rect)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.IsValidFolder(path)) return;
            if (_folderRuleMap.ContainsKey(path))
            {
                CFD_ProjectFolderColorRule rule = _folderRuleMap[path];
                DrawWithVisualConfig(rule.VisualConfig, rect, guid, path);
            }
            else
            {
                ResolvePathRule(rect, guid, path);
            }
            //CFD_ProjectFolderColorRule rule = _folderRuleMap[path];
        }

        private static void ResolvePathRule(Rect rect, string guid, string path)
        {
            foreach (CFD_ProjectPathColorRule rule in _config.PathRules)
            {
                if (rule.PathRule.Type == CFD_PathCheckType.Contains && path.Contains(rule.PathRule.Value))
                {
                    DrawWithVisualConfig(rule.Visual, rect, guid, path);
                    return;
                }
                else if (rule.PathRule.Type == CFD_PathCheckType.EndsWith && path.EndsWith(rule.PathRule.Value))
                {
                    DrawWithVisualConfig(rule.Visual, rect, guid, path);
                    return;
                }
                else if (rule.PathRule.Type == CFD_PathCheckType.StartsWith && path.StartsWith(rule.PathRule.Value))
                {
                    DrawWithVisualConfig(rule.Visual, rect, guid, path);
                    return;
                }
            }
        }

        private static void DrawWithVisualConfig(CFD_VisualConfig visualRuleSet, Rect rect, string guid, string path)
        {
            Rect area = new Rect(0, rect.y, rect.width + rect.x, rect.height);

            DrawGradient(area, visualRuleSet.ColorInfo.StartColor, visualRuleSet.ColorInfo.EndColor);

            if (visualRuleSet.Text.OverrideFont)
            {
                string label = "";
                if (visualRuleSet.Text.OverrideLabel)
                {
                    label = visualRuleSet.Text.Label;
                }
                else
                {
                    var parts = path.Split("/");
                    label = parts[^1];
                }

                DrawLabel(rect, visualRuleSet.Text.TextOffset, label, visualRuleSet.Text.Color, visualRuleSet.Text.Style);
            }

            if (visualRuleSet.Selection.OverrideSelectionColor)
            {
                if (Selection.assetGUIDs.Contains(guid))
                    DrawSelection(rect, true, visualRuleSet.Selection.Color);
            }

            if (visualRuleSet.Icon.Enable)
            {
                DrawIcon(rect, visualRuleSet.Icon.Texture, visualRuleSet.Icon.Size, visualRuleSet.Icon.OffsetScaleX, visualRuleSet.Icon.PixelOffsetX, visualRuleSet.Icon.PixelOffsetY);
            }

            if (visualRuleSet.Marker.Enable)
            {
                DrawIcon(rect, visualRuleSet.Marker.Texture, visualRuleSet.Marker.Size, visualRuleSet.Marker.OffsetScaleX, visualRuleSet.Marker.PixelOffsetX, visualRuleSet.Marker.PixelOffsetY);
            }
        }

        private static void DrawGradient(Rect rect, Color startColor, Color endColor)
        {
            Matrix4x4 matrix = GUI.matrix;
            Rect gradientRect = rect;

            for (int i = 0; i < rect.width; i++)
            {
                var t = i / rect.width;
                var color = Color.Lerp(startColor, endColor, t);
                gradientRect.x = rect.x + i;
                gradientRect.width = 1;

                EditorGUI.DrawRect(gradientRect, color);
            }

            GUI.matrix = matrix;
        }

        private static void DrawLabel(Rect rect, float offset, string label, Color fontColor, FontStyle fontStyle)
        {
            GUIStyle style = new GUIStyle()
            {
                normal = new GUIStyleState() { textColor = fontColor },
                fontStyle = fontStyle
            };

            rect.x += offset;

            EditorGUI.LabelField(rect, label, style);
        }

        static void DrawSelection(Rect inSelectionRect, bool isSelected, Color selectionColor)
        {
            if (isSelected)
            {
                Rect backgroundRect = inSelectionRect;
                backgroundRect.x = 0;
                backgroundRect.xMax *= 1.5f;

                EditorGUI.DrawRect(backgroundRect, selectionColor);
            }
        }

        private static void DrawIcon(Rect rect, Texture tex, Vector2 iconSize, float offsetX, float pixelOffsetX, float pixelOffsetY)
        {
            Rect r;
            float weight = 0, height = 0;
            float w = iconSize.x;
            float h = iconSize.y;

            if (w <= 0) weight = 16; else weight = w;
            if (h <= 0) height = 16; else height = h;

            //if (offsetX < 0)
            //    r = new Rect(rect.x + rect.width + pixelOffsetX, rect.y, weight, height);
            //else
            r = new Rect((rect.x) * offsetX + pixelOffsetX, rect.y + pixelOffsetY, weight, height);

            GUI.DrawTexture(r, tex);
        }
    }
}
#endif