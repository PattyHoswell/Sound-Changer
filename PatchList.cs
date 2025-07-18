using HarmonyLib;
using I2.Loc;
using ShinyShoe.Audio;
using ShinyShoe.Loading;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SoundManager;

namespace Patty_SoundChanger_MOD
{
    internal class PatchList
    {
        [HarmonyPostfix, HarmonyPatch(typeof(ShinyShoe.AppManager), "DoesThisBuildReportErrors")]
        public static void DisableErrorReportingPatch(ref bool __result)
        {
            __result = false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(AudioClip), nameof(AudioClip.LoadAudioData))]
        public static bool LoadAudioData(AudioClip __instance, ref bool __result)
        {
            if (__instance.name.Contains(PluginInfo.GUID))
            {
                __result = true;
                return false;
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(AudioClip), nameof(AudioClip.UnloadAudioData))]
        public static bool UnloadAudioData(AudioClip __instance, ref bool __result)
        {
            if (__instance.name.Contains(PluginInfo.GUID))
            {
                __result = true;
                return false;
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(SoundManager), nameof(SoundManager.currentTrackName), MethodType.Setter)]
        public static void CurrentTrackName_Set(SoundManager __instance, CoreAudioSystem ___audioSystem, ref string value)
        {
            var modifiedName = Plugin.GetModifiedAudioName(value, ___audioSystem);
            if (!string.IsNullOrEmpty(modifiedName))
            {
                value = modifiedName;
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(LoadScreen), "StartLoadingScreen")]
        public static void StartLoadingScreen(LoadScreen __instance)
        {
            Plugin.CreateEntries();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SoundManager), "PlayMusic")]
        public static void PlayMusic(SoundManager __instance,
                                     string trackName,
                                     List<BattleMusicTrack> ___battleMusicTracks,
                                     List<BattleMusicTrack> ___bossBattleMusicTracks,
                                     CoreAudioSystem ___audioSystem)
        {
            var localizedText = __instance.currentTrackName;
            var modifiedName = Plugin.GetModifiedAudioName(trackName, ___audioSystem);
            if (!string.IsNullOrEmpty(modifiedName))
            {
                localizedText = modifiedName;
            }
            else
            {
                var battleMusics = ___battleMusicTracks.Union(___bossBattleMusicTracks);
                var musicTrack = battleMusics.FirstOrDefault(track => track.trackNameData == trackName);
                if (!string.IsNullOrEmpty(musicTrack.trackNameData))
                {
                    localizedText = musicTrack.publicTrackNameKey;
                }
            }
            if (string.IsNullOrEmpty(localizedText))
            {
                localizedText = trackName;
            }
            else if (LocalizationManager.IsTranslatableTerm(localizedText))
            {
                localizedText = LocalizationManager.GetTranslation(localizedText);
            }
            else if (LocalizationManager.IsTranslatableTerm(localizedText))
            {
                localizedText = LocalizationManager.GetTranslation(trackName);
            }
            Plugin.musicChanged.Dispatch(localizedText);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SoundManager), "PlayBattleMusic")]
        public static void PlayBattleMusic(SoundManager __instance)
        {
            var localizedText = __instance.currentTrackName;
            if (LocalizationManager.IsTranslatableTerm(localizedText))
            {
                localizedText = LocalizationManager.GetTranslation(localizedText);
            }
            Plugin.musicChanged.Dispatch(localizedText);
        }
    }
}
