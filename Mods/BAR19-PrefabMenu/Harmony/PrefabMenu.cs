using DMT;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

public class PrefabMenu
{
  public class PrefabMenu_Init : IHarmony
  {
    public void Start()
    {
        Debug.Log(" Loading Patch: " + GetType().ToString());
        var harmony = new Harmony(GetType().ToString());
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
  }
  
  // Could patch the delegate's internal method directly ( PlayerMoveController.<>c__DisplayClass32_0.<Awake>b__21() ),
  // but the name can change each update.  This is simpler than searching for the correct one programmatically
  [HarmonyPatch(typeof(PlayerMoveController))]
  [HarmonyPatch("Awake")]
  public class Patch_PlayerMoveController_Awake
  {
    static void Postfix(PlayerMoveController __instance, ref List<NGuiAction> ___globalActions)
    {
      // Can't use refs for these, since they are being used inside of delegates
      GUIWindowManager _windowManager = AccessTools.Field(typeof(PlayerMoveController), "windowManager").GetValue(__instance) as GUIWindowManager;
      GameManager _gameManager = AccessTools.Field(typeof(PlayerMoveController), "gameManager").GetValue(__instance) as GameManager;
      
      foreach (NGuiAction nGuiAction in ___globalActions)
      {
        if (nGuiAction.GetText() == "Prefab") {
          // We found the correct action, time to restore the old code
          NGuiAction.IsEnabledDelegate menuIsEnabled = delegate
          {
            if (!XUiC_SpawnSelectionWindow.IsOpenInUI(LocalPlayerUI.primaryUI) && _gameManager.gameStateManager.IsGameStarted() && GameStats.GetInt(EnumGameStats.GameState) == 1 && !LocalPlayerUI.primaryUI.windowManager.IsModalWindowOpen())
            {
              return _windowManager.IsHUDEnabled();
            }
            return false;
          };
          
          nGuiAction.SetIsEnabledDelegate(delegate
          {
            if (menuIsEnabled())
            {
              return _gameManager.IsEditMode() || GamePrefs.GetBool(EnumGamePrefs.DebugMenuEnabled);
            }
            return false;
          });
          
          break;
        }
      }
    }
  }
}
