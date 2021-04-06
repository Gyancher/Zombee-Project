using DMT;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

public class Gyancher_ExtraSupplyCratesPatch
{ 
    public class Gyancher_ExtraSupplyCrates_Logger
    {
        public static bool blDisplayLog = true;

        public static void Log(String strMessage)
        {
            if (blDisplayLog)
                UnityEngine.Debug.Log(strMessage);
        }
    }
	
	public class Gyancher_ExtraSupplyCrates_Init : IHarmony
    {
        public void Start()
        {
			Gyancher_ExtraSupplyCrates_Logger.Log(" Loading Patch: " + this.GetType().ToString());
			var harmony = new Harmony("Gyancher.ExtraSupplyCrates.Patch");
			harmony.PatchAll();
        }
    }
	
	[HarmonyPatch(typeof(AIDirectorAirDropComponent), "SpawnSupplyCrate")]
	public class Gyancher_SpawnSupplyCrate_Patch
    {				
		public static bool Prefix(AIDirectorAirDropComponent __instance, Vector3 spawnPos, ref List<int> ___supplyCrates) //___supplyCrates is a private field but we can access it with ___
		{
			String strSpawnGroup = "SupplyCrates_General";
			int classID = 0;
			
			if (GameManager.Instance.World == null)
			{
				return false;
			}
			if (___supplyCrates.Count >= 12)
			{
				Entity entity = GameManager.Instance.World.GetEntity(___supplyCrates[0]); //__instance is not needed due to the local reference to supplyCrates
				if (entity != null)
				{
					entity.MarkToUnload();
				}
				___supplyCrates.RemoveAt(0);
			}

			Entity entity2 = EntityFactory.CreateEntity(EntityGroups.GetRandomFromGroup(strSpawnGroup, ref classID), spawnPos, new Vector3(UnityEngine.Random.Range(0, 10) * 360f, 0f, 0f));
			GameManager.Instance.World.SpawnEntityInWorld(entity2);
			___supplyCrates.Add(entity2.entityId);
		
			return false;
		}
	}
	
}