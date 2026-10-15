// Invoke the actual import rule, with declarations only for Unity infrastructure.
using System;
using System.IO;
using System.Reflection;
using Starfall.Editor;
using UnityEditor;
using UnityEngine;
public static class EditorImportChecks
{
    private static readonly string[] AcceptedNative = {
        "Monsters", "WalkMonsters", "Bosses", "WalkBossA",
        "WalkBossB", "Npcs", "EnemyArcherVertical",
        "CompanionMira", "CompanionIvo", "CompanionSen",
        "WalkMira", "WalkIvo", "WalkSen"
    };
    public static int Main()
    {
        int count=0;
        foreach(var path in Directory.GetFiles("Assets/Resources/Frontier/Art","*.png",SearchOption.AllDirectories))
        {
            if(path.EndsWith("/TitleVista.png")||path.EndsWith("/Portraits.png"))continue;
            var rule=new FrontierImports();var texture=new TextureImporter();rule.assetPath=path.Replace('\\','/');rule.assetImporter=texture;
            typeof(FrontierImports).GetMethod("OnPreprocessTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(rule,null);
            string name=Path.GetFileNameWithoutExtension(path);
            bool readable=(name.StartsWith("Player")&&name!="PlayerPortraits")||name.StartsWith("Walk")||name.StartsWith("Companion")||name=="Monsters"||name=="Bosses"||name=="EnemyArcherVertical";
            if(texture.isReadable!=readable||texture.filterMode!=FilterMode.Point||texture.mipmapEnabled||texture.maxTextureSize!=4096||texture.textureCompression!=TextureImporterCompression.Uncompressed||texture.textureType!=TextureImporterType.Default)throw new Exception("Fresh native import rule contradicts fixed pixel metadata: "+path);
            // Simulate the reimport that the real accepted-actor builder
            // performs. The previous postprocessor silently undid Sprite
            // import and made every accepted Animator controller impossible.
            var imported=new TextureImporter { textureType=TextureImporterType.Sprite };
            rule.assetImporter=imported;
            typeof(FrontierImports).GetMethod("OnPreprocessTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(rule,null);
            bool accepted=Array.IndexOf(AcceptedNative,name)>=0;
            if((imported.textureType==TextureImporterType.Sprite)!=accepted)
                throw new Exception("Accepted native Sprite/import protection mismatch: "+path);
            if(imported.filterMode!=FilterMode.Point||imported.mipmapEnabled||
               imported.maxTextureSize!=4096||imported.textureCompression!=TextureImporterCompression.Uncompressed||
               imported.isReadable!=readable)
                throw new Exception("Native actor import lost original pixel/readability protections: "+path);
            count++;
        }
        Console.WriteLine("PASS Production Editor import rule: "+count+" textures, 13 accepted Sprite/Multiple-ready identities, other images held at Default, sources readable, Point/no mipmaps/uncompressed");return 0;
    }
}
