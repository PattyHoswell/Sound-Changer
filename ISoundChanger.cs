using BepInEx.Configuration;
using ShinyShoe.Audio;
using System;
using System.Collections.Generic;
using UnityEngine;
using static ShinyShoe.Audio.CoreMusicData;
using static ShinyShoe.Audio.CoreSoundEffectData;
using static SoundManager;

namespace Patty_SoundChanger_MOD
{
    /// <summary>
    /// The actual implementation is on <see cref="SoundChangerManager"/>
    /// </summary>
    public interface ISoundChanger
    {
        /// <summary>
        /// Create an empty music entry with no definition set
        /// </summary>
        /// <param name="entryName"></param>
        /// <param name="description"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        SoundData<MusicDefinition> CreateNewMusicEntry(string entryName, string description = "", string defaultVal = "");

        /// <summary>
        /// Create an empty SFX entry with no definition set
        /// </summary>
        /// <param name="entryName"></param>
        /// <param name="description"></param>
        /// <param name="defaultVal"></param>
        /// <returns></returns>
        SoundData<SoundCueDefinition> CreateNewSFXEntry(string entryName, string description = "", string defaultVal = "");

        /// <summary>
        /// Register a music entry to battle music, if <paramref name="isBoss"/> is <see langword="true"/> then it will only play the music if its called with that name
        /// </summary>
        /// <param name="musicData"></param>
        /// <param name="isBoss"></param>
        /// <returns></returns>
        BattleMusicTrack RegisterToBattleMusic(SoundData<MusicDefinition> musicData, bool isBoss = false);

        /// <summary>
        /// Register a music entry to the musics definition in the game. No need to call this if you already use <see cref="RegisterToBattleMusic(SoundData{MusicDefinition}, bool)"/>
        /// </summary>
        /// <param name="musicData"></param>
        void RegisterMusicEntry(SoundData<MusicDefinition> musicData);

        /// <summary>
        /// Get the entry with that name
        /// </summary>
        /// <param name="entryName"></param>
        /// <returns></returns>
        ConfigEntry<string> GetEntry(string entryName);

        /// <summary>
        /// Check whether the entry is a valid music entry
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        bool IsMusicEntry(ConfigEntryBase entry);

        /// <summary>
        /// Check whether the entry is a valid SFX entry
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        bool IsSFXEntry(ConfigEntryBase entry);

        /// <summary>
        /// Check whether the entry is a valid music or SFX entry
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        bool IsValidEntry(ConfigEntryBase entry);

        /// <summary>
        /// Get music data with that name
        /// </summary>
        /// <param name="entryName"></param>
        /// <returns></returns>
        SoundData<MusicDefinition> GetMusicData(string entryName);

        /// <summary>
        /// Get music data with that entry
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        SoundData<MusicDefinition> GetMusicData(ConfigEntryBase entry);

        /// <summary>
        /// Get SFX data with that name
        /// </summary>
        /// <param name="entryName"></param>
        /// <returns></returns>
        SoundData<SoundCueDefinition> GetSFXData(string entryName);

        /// <summary>
        /// Get SFX data with that entry
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        SoundData<SoundCueDefinition> GetSFXData(ConfigEntryBase entry);

        /// <summary>
        /// Get the list of battle music tracks. Note that this is the original instance of the list, changing this will also change the game list
        /// </summary>
        /// <param name="isBoss"></param>
        /// <returns></returns>
        List<BattleMusicTrack> GetBattleMusicTracks(bool isBoss = false);

        /// <summary>
        /// Get a battle music track with that name
        /// </summary>
        /// <param name="trackName"></param>
        /// <returns></returns>
        BattleMusicTrack GetBattleMusicTrack(string trackName);

        /// <summary>
        /// Get the sound manager
        /// </summary>
        /// <returns></returns>
        SoundManager GetSoundManager();

        /// <summary>
        /// Get the core audio system
        /// </summary>
        /// <returns></returns>
        CoreAudioSystem GetCoreAudioSystem();

        /// <summary>
        /// Get the core audio system data
        /// </summary>
        /// <returns></returns>
        CoreAudioSystemData GetCoreAudioSystemData();

        /// <summary>
        /// Get all of the music definitions in game. Note that this is the original instance of the array, changing this will also change the game array
        /// </summary>
        /// <returns></returns>
        MusicDefinition[] GetMusicDefinitions();

        /// <summary>
        /// Get all of the SFX definitions in game. Note that this is the original instance of the array, changing this will also change the game array
        /// </summary>
        /// <returns></returns>
        SoundCueDefinition[] GetSFXDefinitions();

        /// <summary>
        /// Get music definition with that name
        /// </summary>
        /// <param name="trackName"></param>
        /// <returns></returns>
        MusicDefinition GetMusicDefinition(string trackName);

        /// <summary>
        /// Get SFX definition with that name
        /// </summary>
        /// <param name="trackName"></param>
        /// <returns></returns>
        SoundCueDefinition GetSFXDefinition(string trackName);

        /// <summary>
        /// Change an entry sound to that file
        /// </summary>
        /// <param name="entryName"></param>
        /// <param name="filePath"></param>
        /// <param name="replayMusic"></param>
        /// <param name="onPlay"></param>
        void ChangeSound(string entryName, string filePath, bool replayMusic = true, Action<string> onPlay = null);

        /// <summary>
        /// Change an entry sound to that AudioClip.
        /// <br/><br/>
        /// WARNING: The <paramref name="audioClip"/> should have format like <see cref="PluginInfo.GUID"/>_TheActualAudioClipName. 
        /// This is so that the mod can prevent the game from unloading the data of the audio clip, or you can make your own code to disable this
        /// </summary>
        /// <param name="entryName"></param>
        /// <param name="audioClip"></param>
        /// <param name="replayMusic"></param>
        /// <param name="onPlay"></param>
        void ChangeSound(string entryName, AudioClip audioClip, bool replayMusic = true, Action<string> onPlay = null);

        /// <summary>
        /// Change an entry sound to that file
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="filePath"></param>
        /// <param name="replayMusic"></param>
        /// <param name="onPlay"></param>
        void ChangeSound(ConfigEntryBase entry, string filePath, bool replayMusic = true, Action<string> onPlay = null);

        /// <summary>
        /// Change an entry sound to that AudioClip.
        /// <br/><br/>
        /// WARNING: The <paramref name="audioClip"/> should have format like <see cref="PluginInfo.GUID"/>_TheActualAudioClipName. 
        /// This is so that the mod can prevent the game from unloading the data of the audio clip, or you can make your own code to disable this
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="audioClip"></param>
        /// <param name="replayMusic"></param>
        /// <param name="onPlay"></param>
        void ChangeSound(ConfigEntryBase entry, AudioClip audioClip, bool replayMusic = true, Action<string> onPlay = null);

        /// <summary>
        /// Play an entry, will play as music if its a vaild music entry, SFX if its a valid sfx entry
        /// </summary>
        /// <param name="entryName"></param>
        /// <param name="onPlay"></param>
        void PlayEntry(string entryName, Action<string> onPlay = null);

        /// <summary>
        /// Play an entry, will play as music if its a vaild music entry, SFX if its a valid sfx entry
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="onPlay"></param>
        void PlayEntry(ConfigEntryBase entry, Action<string> onPlay = null);

        /// <summary>
        /// Play a music entry, <paramref name="isBattleMusic"/> will display <see cref="MusicNotificationHandler"/> if its set to <see langword="true"/>
        /// </summary>
        /// <param name="entryName"></param>
        /// <param name="isBattleMusic"></param>
        /// <param name="crossfadeTimeSeconds"></param>
        /// <param name="displayedName"></param>
        /// <param name="onPlay"></param>
        void PlayMusic(string entryName, bool isBattleMusic = false, float crossfadeTimeSeconds = 0.25f, string displayedName = "", Action<string> onPlay = null);

        /// <summary>
        /// Play a music entry, <paramref name="isBattleMusic"/> will display <see cref="MusicNotificationHandler"/> if its set to <see langword="true"/>
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="isBattleMusic"></param>
        /// <param name="crossfadeTimeSeconds"></param>
        /// <param name="displayedName"></param>
        /// <param name="onPlay"></param>
        void PlayMusic(ConfigEntryBase entry, bool isBattleMusic = false, float crossfadeTimeSeconds = 0.25f, string displayedName = "", Action<string> onPlay = null);

        /// <summary>
        /// Play an SFX entry, will ignore <see cref="SoundCueDefinition.Loop"/> uses <paramref name="loop"/> instead
        /// This is to disable the loop temporarily when playing the SFX through this mod.
        /// </summary>
        /// <param name="entryName"></param>
        /// <param name="loop"></param>
        /// <param name="onPlay"></param>
        void PlaySFX(string entryName, bool loop = false, Action<string> onPlay = null);

        /// <summary>
        /// Play an SFX entry, will ignore <see cref="SoundCueDefinition.Loop"/> uses <paramref name="loop"/> instead
        /// This is to disable the loop temporarily when playing the SFX through this mod.
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="loop"></param>
        /// <param name="onPlay"></param>
        void PlaySFX(ConfigEntryBase entry, bool loop = false, Action<string> onPlay = null);

        /// <summary>
        /// Open the load file dialog for that entry
        /// </summary>
        /// <param name="entryName"></param>
        /// <param name="onLoad"></param>
        /// <param name="onPlay"></param>
        void LoadFileForEntry(string entryName, Action<string, AudioClip> onLoad = null, Action<string> onPlay = null);

        /// <summary>
        /// Open the load file dialog for that entry
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="onLoad"></param>
        /// <param name="onPlay"></param>
        void LoadFileForEntry(ConfigEntryBase entry, Action<string, AudioClip> onLoad = null, Action<string> onPlay = null);

        /// <summary>
        /// Reset the audio clip for that entry
        /// </summary>
        /// <param name="entryName"></param>
        void ResetEntry(string entryName);

        /// <summary>
        /// Reset the audio clip for that entry
        /// </summary>
        /// <param name="entry"></param>
        void ResetEntry(ConfigEntryBase entry);

        /// <summary>
        /// Get the currently playing music name
        /// </summary>
        /// <returns></returns>
        string GetCurrentlyPlayingMusicName();

        /// <summary>
        /// Check whether the SoundManager has been initialized, very important to do this in case you are activating your code too early.
        /// </summary>
        /// <returns></returns>
        bool HasInitializedSoundManager();
    }
}
