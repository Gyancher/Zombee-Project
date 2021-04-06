using DMT;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using SDX.Compiler;
using UnityEngine;

public class HarmonyManager : IHarmony
{

   public void Start()
   {
       Debug.Log(" Loading Patch: " + GetType().ToString());
       var harmony = new Harmony(GetType().ToString());
       harmony.PatchAll(Assembly.GetExecutingAssembly());
   }
}
