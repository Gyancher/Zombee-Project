using DMT;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System;
using SDX.Compiler;
using UnityEngine;

[HarmonyPatch(typeof(PlayerMoveController))]
[HarmonyPatch("Update")]
public class RunAndGunTranspiler 
{

   // Loops around the instructions and removes the return condition.
   static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
   {

	    int index = -1;
        var ins = new List<CodeInstruction>(instructions);
        
		for(int x= 0;x < ins.Count;x++){
			
			var d = ins[x];
			if (d.opcode == OpCodes.Callvirt && d.operand != null && d.operand.ToString().Contains("set_AimingGun")){
				index = x;
				break;
			}
			
		}
		
		if (index < 0){
            Logging.LogWarning("==Run and Gun instruction not found, skipping===");
			return ins.AsEnumerable();
		}
		
        var dargCount = 0;
        while (true)
        {
			
			var instruction = ins[index];
            var op = instruction.opcode;
            ins.Remove(instruction);
            if (op == OpCodes.Ldarg_0)
            {
                if (++dargCount == 2)
                    break;
            }
			index--;
        }

       return ins.AsEnumerable();
   }
}
