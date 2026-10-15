using System;
using System.Globalization;
using UnityEngine;

namespace Starfall
{
    // Source-controlled ACTUAL Unity Resources visual asset, not a generated
    // low-resolution replacement Sprite. Future artists can tune the look
    // without changing the original detailed PNGs or GameCore save schema.
    public sealed partial class FrontierRuntime
    {
        private sealed class WorldVisualLook
        {
            public float treeSwayDegrees=.80f;
            public float palmSwayDegrees=1.35f;
            public float snowpineSwayDegrees=.24f;
            public float waterGlintAlpha=.12f;
            public float waterGlintRate=.89f;
            public float lampRadius=114f;
            public float nightLightAlpha=.29f;
            public float dayLightAlpha=.13f;
        }
        private WorldVisualLook worldVisualLook;

        private WorldVisualLook CurrentWorldVisualLook
        {
            get
            {
                if (worldVisualLook != null) return worldVisualLook;
                WorldVisualLook settings=new WorldVisualLook();
                TextAsset authored=Resources.Load<TextAsset>(
                    "Frontier/Visual/WorldLook");
                if(authored!=null && !String.IsNullOrEmpty(authored.text))
                {
                    foreach(string line in authored.text.Split('\n'))
                    {
                        string text=line.Trim();
                        if(text.Length==0 || text[0]=='#')continue;
                        int separator=text.IndexOf('=');
                        if(separator<=0)continue;
                        string key=text.Substring(0,separator).Trim();
                        string literal=text.Substring(separator+1).Trim();
                        float value;
                        if(!Single.TryParse(literal,NumberStyles.Float,
                                CultureInfo.InvariantCulture,out value) ||
                           Single.IsNaN(value)||Single.IsInfinity(value))
                            continue;
                        // Deliberately bounded per-property: art direction
                        // changes must never hide important actors/UI or
                        // cause disorienting sway/rapid flashing.
                        switch(key)
                        {
                            case "treeSwayDegrees":
                                if(value>=0&&value<=2.5f)
                                    settings.treeSwayDegrees=value;break;
                            case "palmSwayDegrees":
                                if(value>=0&&value<=3f)
                                    settings.palmSwayDegrees=value;break;
                            case "snowpineSwayDegrees":
                                if(value>=0&&value<=1f)
                                    settings.snowpineSwayDegrees=value;break;
                            case "waterGlintAlpha":
                                if(value>=0&&value<=.18f)
                                    settings.waterGlintAlpha=value;break;
                            case "waterGlintRate":
                                if(value>=.1f&&value<=2f)
                                    settings.waterGlintRate=value;break;
                            case "lampRadius":
                                if(value>=90f&&value<=150f)
                                    settings.lampRadius=value;break;
                            case "nightLightAlpha":
                                if(value>=.05f&&value<=.36f)
                                    settings.nightLightAlpha=value;break;
                            case "dayLightAlpha":
                                if(value>=0f&&value<=.20f)
                                    settings.dayLightAlpha=value;break;
                        }
                    }
                }
                worldVisualLook=settings;
                return settings;
            }
        }
    }
}
