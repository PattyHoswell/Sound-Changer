using HarmonyLib;
using ShinyShoe;

namespace Patty_SoundChanger_MOD
{
    internal class MenuPatch
    {
        [HarmonyPrefix, HarmonyPatch(typeof(SettingsScreen), "ApplyScreenInput")]
        public static bool SettingsScreen_ApplyScreenInput(CoreInputControlMapping mapping,
                                                           IGameUIComponent triggeredUI,
                                                           InputManager.Controls triggeredMappingID,
                                                           ref bool __result)
        {
            if (SoundDialog.Instance == null)
            {
                return true;
            }
            if (SoundDialog.Instance.ApplyScreenInput(mapping, triggeredUI, triggeredMappingID))
            {
                __result = true;
                return false;
            }
            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(PauseDialog), "ApplyScreenInput")]
        public static void PauseDialog_ApplyScreenInput(CoreInputControlMapping mapping,
                                                        IGameUIComponent triggeredUI,
                                                        InputManager.Controls triggeredMappingID,
                                                        ref bool __result)
        {
            if (!__result && SoundDialog.Instance != null)
            {
                __result = SoundDialog.Instance.ApplyScreenInput(mapping, triggeredUI, triggeredMappingID);
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(SettingsScreen), "CloseDialog")]
        public static bool CloseDialog()
        {
            if (SoundDialog.Instance != null && SoundDialog.Instance.Active)
            {
                SoundDialog.Instance.Close();
                return false;
            }
            return true;
        }
    }
}
