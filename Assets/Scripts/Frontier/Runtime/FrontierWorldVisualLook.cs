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
            public float groundFoliageSwayDegrees=.65f;
            public float waterGlintAlpha=.12f;
            public float waterGlintRate=.89f;
            public float waterRippleAlpha=.21f;
            public float waterRippleFrameRate=4.4f;
            public float shorelineWashAlpha=.10f;
            public float shorelineTravelPixels=1.5f;
            public float shoreFoamAlpha=.19f;
            public float shoreFoamFrameRate=3.6f;
            public float emberFlickerAlpha=.20f;
            public float emberLiftPixels=7f;
            public float campfireFlameAlpha=.70f;
            public float campfireFlameFrameRate=5f;
            public float relicShimmerAlpha=.24f;
            public float relicShimmerRate=3.8f;
            public float relicHaloRadius=83f;
            public float relicHaloAlpha=.18f;
            // Approved illustrated Effects.png air movement. Artist-editable
            // Resources fields control density/flow without touching hero art,
            // quest data or inventing new standalone sprite animations.
            public float ambientMoteDayAlpha=.13f;
            public float ambientMoteNightAlpha=.21f;
            public float snowMoteWindX=.13f;
            public float desertMoteWindX=.16f;
            public float coastMoteLift=.12f;
            public float lampRadius=114f;
            public float nightLightAlpha=.29f;
            public float dayLightAlpha=.13f;
            public float npcSignalSpeechAlpha=.70f;
            public float npcSignalDangerAlpha=.78f;
            public float npcSignalFrameRate=4f;
            public float npcTalkEntrySeconds=.18f;
            public float npcTalkLeanDegrees=.72f;
            public float escortWalkLeanDegrees=1.15f;
            public float escortHitLeanDegrees=1.35f;
            public float combatWarningStrokePixels=1.8f;
            // Forward foreground warning outlines must stay visible without
            // an opaque high-saturation rim over the canonical actor's art.
            // Countdown text intentionally has a stronger separate value.
            public float hazardRingAlpha=.80f;
            public float hazardCountdownAlpha=.94f;
            public float projectileTrailFadeDistance=.18f;
            public float projectileTrailAlpha=.42f;
            public float projectileTrailFrameRate=9f;
            public float castSigilAlpha=.68f;
            public float castSigilBossPixels=58f;
            public float hitContactBlendFraction=.16f;
            public float slashBridgeFraction=.18f;
            // Artist-facing, bounded transitions for whole painted enemy
            // bodies and phase-cue particles, never GameCore attack timing.
            public float nonheroAtlasHandoffSeconds=.105f;
            public float bossPhaseMoteAlpha=.26f;
            public float impactFlourishAlpha=.58f;
            public float deathPoseBlendFraction=.24f;
            // Source-authored companion cadence, not generated anatomy.
            // Each quarter-cycle uses an original full-body 256px cell.
            public float companionStrideDistanceFactor=1.20f;
            public float companionFootfallSeconds=.075f;
            public float companionFacingThreshold=.15f;
            public float companionAttackPlantSeconds=.065f;
            // Brief, accepted Effects.png soil contacts driven by real steps.
            // These never manufacture an extra walk/body animation.
            public float footfallAlpha=.26f;
            public float footfallLifetime=.32f;
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
                            case "groundFoliageSwayDegrees":
                                if(value>=0&&value<=1.3f)
                                    settings.groundFoliageSwayDegrees=value;break;
                            case "waterGlintAlpha":
                                if(value>=0&&value<=.18f)
                                    settings.waterGlintAlpha=value;break;
                            case "waterGlintRate":
                                if(value>=.1f&&value<=2f)
                                    settings.waterGlintRate=value;break;
                            case "waterRippleAlpha":
                                if(value>=.06f&&value<=.36f)
                                    settings.waterRippleAlpha=value;break;
                            case "waterRippleFrameRate":
                                if(value>=1.5f&&value<=7f)
                                    settings.waterRippleFrameRate=value;break;
                            case "shorelineWashAlpha":
                                if(value>=.02f&&value<=.18f)
                                    settings.shorelineWashAlpha=value;break;
                            case "shoreFoamAlpha":
                                if(value>=.07f&&value<=.27f)
                                    settings.shoreFoamAlpha=value;break;
                            case "shoreFoamFrameRate":
                                if(value>=1.8f&&value<=5.5f)
                                    settings.shoreFoamFrameRate=value;break;
                            case "shorelineTravelPixels":
                                if(value>=0f&&value<=2.5f)
                                    settings.shorelineTravelPixels=value;break;
                            case "emberFlickerAlpha":
                                if(value>=.04f&&value<=.32f)
                                    settings.emberFlickerAlpha=value;break;
                            case "emberLiftPixels":
                                if(value>=0f&&value<=14f)
                                    settings.emberLiftPixels=value;break;
                            case "campfireFlameAlpha":
                                if(value>=.24f&&value<=.84f)
                                    settings.campfireFlameAlpha=value;break;
                            case "campfireFlameFrameRate":
                                if(value>=2f&&value<=8f)
                                    settings.campfireFlameFrameRate=value;break;
                            case "relicShimmerAlpha":
                                if(value>=.08f&&value<=.32f)
                                    settings.relicShimmerAlpha=value;break;
                            case "relicShimmerRate":
                                if(value>=1.8f&&value<=6f)
                                    settings.relicShimmerRate=value;break;
                            case "relicHaloRadius":
                                if(value>=56f&&value<=108f)
                                    settings.relicHaloRadius=value;break;
                            case "relicHaloAlpha":
                                if(value>=.07f&&value<=.26f)
                                    settings.relicHaloAlpha=value;break;
                            case "ambientMoteDayAlpha":
                                if(value>=.04f&&value<=.18f)
                                    settings.ambientMoteDayAlpha=value;break;
                            case "ambientMoteNightAlpha":
                                if(value>=.10f&&value<=.24f)
                                    settings.ambientMoteNightAlpha=value;break;
                            case "snowMoteWindX":
                                if(value>=.04f&&value<=.20f)
                                    settings.snowMoteWindX=value;break;
                            case "desertMoteWindX":
                                if(value>=.05f&&value<=.22f)
                                    settings.desertMoteWindX=value;break;
                            case "coastMoteLift":
                                if(value>=.04f&&value<=.17f)
                                    settings.coastMoteLift=value;break;
                            case "lampRadius":
                                if(value>=90f&&value<=150f)
                                    settings.lampRadius=value;break;
                            case "nightLightAlpha":
                                if(value>=.05f&&value<=.36f)
                                    settings.nightLightAlpha=value;break;
                            case "dayLightAlpha":
                                if(value>=0f&&value<=.20f)
                                    settings.dayLightAlpha=value;break;
                            // Whole-body NPC movement never changes the
                            // underlying 128px source painting or world root.
                            case "npcSignalSpeechAlpha":
                                if(value>=.2f&&value<=.9f)
                                    settings.npcSignalSpeechAlpha=value;break;
                            case "npcSignalDangerAlpha":
                                if(value>=.3f&&value<=.95f)
                                    settings.npcSignalDangerAlpha=value;break;
                            case "npcSignalFrameRate":
                                if(value>=2f&&value<=6f)
                                    settings.npcSignalFrameRate=value;break;
                            case "npcTalkEntrySeconds":
                                if(value>=.08f&&value<=.30f)
                                    settings.npcTalkEntrySeconds=value;break;
                            case "npcTalkLeanDegrees":
                                if(value>=0f&&value<=1.5f)
                                    settings.npcTalkLeanDegrees=value;break;
                            case "escortWalkLeanDegrees":
                                if(value>=0f&&value<=1.8f)
                                    settings.escortWalkLeanDegrees=value;break;
                            case "escortHitLeanDegrees":
                                if(value>=0f&&value<=2f)
                                    settings.escortHitLeanDegrees=value;break;
                            case "combatWarningStrokePixels":
                                if(value>=1.25f&&value<=2.7f)
                                    settings.combatWarningStrokePixels=value;break;
                            case "hazardRingAlpha":
                                if(value>=.55f&&value<=.88f)
                                    settings.hazardRingAlpha=value;break;
                            case "hazardCountdownAlpha":
                                if(value>=.8f&&value<=1f)
                                    settings.hazardCountdownAlpha=value;break;
                            case "projectileTrailFadeDistance":
                                if(value>=.08f&&value<=.32f)
                                    settings.projectileTrailFadeDistance=value;break;
                            case "projectileTrailAlpha":
                                if(value>=.20f&&value<=.50f)
                                    settings.projectileTrailAlpha=value;break;
                            case "projectileTrailFrameRate":
                                if(value>=4f&&value<=14f)
                                    settings.projectileTrailFrameRate=value;break;
                            case "castSigilAlpha":
                                if(value>=.35f&&value<=.85f)
                                    settings.castSigilAlpha=value;break;
                            case "castSigilBossPixels":
                                if(value>=40f&&value<=72f)
                                    settings.castSigilBossPixels=value;break;
                            case "hitContactBlendFraction":
                                if(value>=.08f&&value<=.24f)
                                    settings.hitContactBlendFraction=value;break;
                            case "slashBridgeFraction":
                                if(value>=.10f&&value<=.25f)
                                    settings.slashBridgeFraction=value;break;
                            case "nonheroAtlasHandoffSeconds":
                                if(value>=.07f&&value<=.15f)
                                    settings.nonheroAtlasHandoffSeconds=value;break;
                            case "bossPhaseMoteAlpha":
                                if(value>=.12f&&value<=.32f)
                                    settings.bossPhaseMoteAlpha=value;break;
                            case "impactFlourishAlpha":
                                if(value>=.2f&&value<=.75f)
                                    settings.impactFlourishAlpha=value;break;
                            case "deathPoseBlendFraction":
                                if(value>=.12f&&value<=.35f)
                                    settings.deathPoseBlendFraction=value;break;
                            case "companionStrideDistanceFactor":
                                if(value>=.85f&&value<=1.60f)
                                    settings.companionStrideDistanceFactor=value;break;
                            case "companionFootfallSeconds":
                                if(value>=.05f&&value<=.12f)
                                    settings.companionFootfallSeconds=value;break;
                            case "companionFacingThreshold":
                                if(value>=.06f&&value<=.22f)
                                    settings.companionFacingThreshold=value;break;
                            case "companionAttackPlantSeconds":
                                if(value>=.025f&&value<=.10f)
                                    settings.companionAttackPlantSeconds=value;break;
                            case "footfallAlpha":
                                if(value>=0f&&value<=.45f)
                                    settings.footfallAlpha=value;break;
                            case "footfallLifetime":
                                if(value>=.16f&&value<=.50f)
                                    settings.footfallLifetime=value;break;
                        }
                    }
                }
                worldVisualLook=settings;
                return settings;
            }
        }
    }
}
