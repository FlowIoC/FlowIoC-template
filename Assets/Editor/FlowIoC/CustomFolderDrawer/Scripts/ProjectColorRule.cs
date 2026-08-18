#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEngine;

namespace Editor.FlowIoC.CustomFolderDrawer.Scripts
{
    [Serializable]
    public class CFD_ProjectPathColorRule
    {
        public CFD_PathRule PathRule;
        public CFD_VisualConfig Visual;
    }

    [Serializable]
    public class CFD_ProjectFolderColorRule
    {
        public CFD_FolderRule FolderRule;
        public CFD_VisualConfig VisualConfig;
    }

    [Serializable]
    public class CFD_VisualConfig
    {
        public CFD_ColorConfig ColorInfo;
        public CFD_SelectionConfig Selection;
        public CFD_LabelConfig Text;
        public CFD_IconConfig Icon;
        public CFD_IconConfig Marker;
    }

    [Serializable]
    public class CFD_PathRule
    {
        public string Value;
        public CFD_PathCheckType Type;
    }

    public enum CFD_PathCheckType
    {
        Contains,
        EndsWith,
        StartsWith
    }

    [Serializable]
    public class CFD_FolderRule
    {
        public DefaultAsset Folder;

        public string Path => AssetDatabase.GetAssetPath(Folder);
    }

    [Serializable]
    public class CFD_ColorConfig
    {
        public Color StartColor;
        public Color EndColor;
    }

    [Serializable]
    public class CFD_SelectionConfig
    {
        public bool OverrideSelectionColor;
        public Color Color;
    }

    [Serializable]
    public class CFD_LabelConfig
    {
        public bool OverrideFont;
        public Color Color;
        public FontStyle Style;
        public float TextOffset = 18.5f;

        public bool OverrideLabel;
        public string Label;
    }

    [Serializable]
    public class CFD_IconConfig
    {
        public bool Enable;
        public Texture Texture;
        public Vector2 Size;
        public float OffsetScaleX = 1;
        public float PixelOffsetX;
        public float PixelOffsetY;
    }

}
#endif