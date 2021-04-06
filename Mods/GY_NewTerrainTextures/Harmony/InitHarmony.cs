using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using System.Xml;
using DMT;
using UnityEngine;

namespace TormentedEmu_Mods_A19
{
  /// <summary>
  /// Loads a new set of terrain textures(diff, norm, spec) and appends it to the fun pimps terrain texture array
  /// </summary>
  public class GY_NewTerrainTextures : IHarmony
  {
    // Change these settings to your preferences
    public static string MyModFolder = "GY_NewTerrainTextures";
    public static string MyUnityBundle = "GYNewTerrainTextures.unity3d";
    public static string MyUVMappingXml = "GYNewTerrainTexturesUVMapping.xml";
    public static string MyTextureArrayMeshName = "GY_TextureSample";
    public static string MyTextureArrayDiffuse = "ta_NewBlockTest";
    public static string MyTextureArrayNormal = "ta_NewBlockTest";
    public static string MyTextureArraySpecular = "ta_NewBlockTest";

    public static bool MatchUVMappings = true; // true will change all vanilla and mod textures to the size provided by UVBlockSize
    /*
     * 1 = 100% sized texture per block
     * 2 = 50%
     * 4 = 25%
     * 8 = ~10%
     * 
     * Most vanilla blocks are 8, with several being 4
    */
    public static int UVBlockSize = 1; // This will override the uvmapping xml settings


    public void Start()
    {
      var harmony = new Harmony("TormentedEmu.Mods.A19");
      harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    [HarmonyPatch(typeof(TextureAtlasTerrain), "LoadTextureAtlas")]
    public class TextureAtlasTerrain_LoadTextureAtlas
    {
      static bool Prefix(ref bool __result, ref TextureAtlasTerrain __instance, int _idx, ref MeshDescriptionCollection _tac, bool _bLoadTextures)
      {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        string myResourcesPath = Path.Combine(Utils.GetGamePath(), "Mods", MyModFolder, "Resources");
        string myBundlePath = Path.Combine(myResourcesPath, MyUnityBundle);
        AssetBundleManager.Instance.LoadAssetBundle(myBundlePath, false);

        if (TextureAtlasBlocks_LoadTextureAtlas(ref __instance, _idx, _tac, _bLoadTextures))
        {
          __instance.diffuse = new Texture2D[__instance.uvMapping.Length];
          __instance.normal = new Texture2D[__instance.uvMapping.Length];
          __instance.specular = new Texture2D[__instance.uvMapping.Length];

          if (_bLoadTextures)
          {
            for (int i = 0; i < __instance.uvMapping.Length; i++)
            {
              if (__instance.uvMapping[i].textureName != null)
              {
                string text = Utils.RemoveFileExtension(__instance.uvMapping[i].textureName);
                string fileExtension = Utils.GetFileExtension(__instance.uvMapping[i].textureName);

                __instance.diffuse[i] = AssetBundleManager.Instance.Get<Texture2D>("TerrainTextures", __instance.uvMapping[i].textureName);
                if (__instance.diffuse[i] == null)
                {
                  __instance.diffuse[i] = AssetBundleManager.Instance.Get<Texture2D>(myBundlePath, __instance.uvMapping[i].textureName);
                  if (__instance.diffuse[i] == null)
                    throw new Exception("TextureAtlasTerrain: couldn't load diffuse texture '" + __instance.uvMapping[i].textureName + "'");
                }

               
              }
            }
          }
        }

        sw.Stop();
        Log.Out("Loading GY terrain textures completed.  Elapsed time: {0}", sw.Elapsed);

        __result = true;

        return false;
      }

      static bool TextureAtlasBlocks_LoadTextureAtlas(ref TextureAtlasTerrain __instance, int _idx, MeshDescriptionCollection _tac, bool _bLoadTextures)
      {
        try
        {
          XmlNodeList childNodes = new XmlFile(_tac.meshes[_idx].MetaData).XmlDoc.DocumentElement.ChildNodes;
          int highestId = 0;

          foreach (XmlNode xmlNode in childNodes)
          {
            if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name.Equals("uv"))
            {
              XmlElement xmlElement = (XmlElement)xmlNode;
              highestId = Math.Max(highestId, int.Parse(xmlElement.GetAttribute("id")));
            }
          }

          string myResourcesPath = Path.Combine(Utils.GetGamePath(), "Mods", MyModFolder, "Resources");
          string myBundlePath = Path.Combine(myResourcesPath, MyUnityBundle);
          string xmlfile = Path.Combine(myResourcesPath, MyUVMappingXml);
          XmlNodeList modChildNodes = new XmlFile(File.ReadAllBytes(xmlfile)).XmlDoc.DocumentElement.ChildNodes;

          foreach (XmlNode xmlNode in modChildNodes)
          {
            if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name.Equals("uv"))
            {
              XmlElement xmlElement = (XmlElement)xmlNode;
              highestId = Math.Max(highestId, int.Parse(xmlElement.GetAttribute("id")));
            }
          }

          __instance.uvMapping = new UVRectTiling[highestId + 1];

          foreach (XmlNode xmlNode in childNodes)
          {
            if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name.Equals("uv"))
            {
              XmlElement xmlElement2 = (XmlElement)xmlNode;
              int id = int.Parse(xmlElement2.GetAttribute("id"));
              UVRectTiling uvrectTiling = default(UVRectTiling);
              uvrectTiling.FromXML(xmlElement2);
              if (MatchUVMappings)
              {
                uvrectTiling.blockH = UVBlockSize;
                uvrectTiling.blockW = UVBlockSize;
              }
              __instance.uvMapping[id] = uvrectTiling;
            }
          }

          foreach (XmlNode xmlNode in modChildNodes)
          {
            if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name.Equals("uv"))
            {
              XmlElement xmlElement2 = (XmlElement)xmlNode;
              int id = int.Parse(xmlElement2.GetAttribute("id"));
              UVRectTiling uvrectTiling = default(UVRectTiling);
              uvrectTiling.FromXML(xmlElement2);
              if (MatchUVMappings)
              {
                uvrectTiling.blockH = UVBlockSize;
                uvrectTiling.blockW = UVBlockSize;
              }
              __instance.uvMapping[id] = uvrectTiling;
            }
          }
        }
        catch (Exception ex)
        {
          Log.Error(string.Concat(new object[]{ "Parsing file xml file for texture atlas ", _tac.name, " (", _idx, "): ", ex.Message, ")" }));
          Log.Exception(ex);
          Log.Error("Loading of textures aborted due to errors!");
          return false;
        }

        TextureAtlas_LoadTextureAtlas(ref __instance, _idx, _tac, _bLoadTextures);
        return true;
      }

      static bool TextureAtlas_LoadTextureAtlas(ref TextureAtlasTerrain __instance, int _idx, MeshDescriptionCollection _tac, bool _bLoadTextures)
      {
        if (_bLoadTextures)
        {
          __instance.diffuseTexture = _tac.meshes[_idx].TexDiffuse;
          __instance.normalTexture = _tac.meshes[_idx].TexNormal;
          __instance.specularTexture = _tac.meshes[_idx].TexSpecular;
          __instance.emissionTexture = _tac.meshes[_idx].TexEmission;
          __instance.heightTexture = _tac.meshes[_idx].TexHeight;
          __instance.occlusionTexture = _tac.meshes[_idx].TexOcclusion;
          __instance.maskTexture = _tac.meshes[_idx].TexMask;
          __instance.maskNormalTexture = _tac.meshes[_idx].TexMaskNormal;
        }

        return true;
      }
    }
  }
}
