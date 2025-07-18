using HarmonyLib;

namespace Patty_SoundChanger_MOD
{
    internal class TemporaryFix
    {
        [HarmonyPrefix, HarmonyPatch(typeof(MusicNotificationHandler), "OnBattleMusicChanged")]
        public static void OnBattleMusicChanged(ref string trackName)
        {
            trackName = AllGameManagers.Instance.GetSoundManager().currentTrackName;
        }
    }
}
