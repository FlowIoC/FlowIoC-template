
#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;

namespace Editor.FlowIoC.CustomFolderDrawer.Scripts
{
    class ED_CustomFolderConfig : ScriptableObject
    {
        public bool Enabled;
        public List<CFD_ProjectFolderColorRule> FolderRules;
        public List<CFD_ProjectPathColorRule> PathRules;
    }
}

#endif