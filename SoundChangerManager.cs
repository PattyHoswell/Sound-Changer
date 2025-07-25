using BepInEx.Configuration;
using HarmonyLib;
using ShinyShoe.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static ShinyShoe.Audio.CoreMusicData;
using static ShinyShoe.Audio.CoreSoundEffectData;
using static SoundManager;

namespace Patty_SoundChanger_MOD
{
    /// <summary>
    /// A publicized class and methods available for your mod to use
    /// </summary>
    public sealed class SoundChangerManager : ISoundChanger
    {
        /// <inheritdoc/>
        public SoundData<MusicDefinition> CreateNewMusicEntry(string entryName, string description = "", string defaultVal = "")
        {
            var duplicatedEntry = Plugin.MusicEntries.Keys.FirstOrDefault(musicEntry => musicEntry.Definition.Key == entryName);
            if (duplicatedEntry != null)
            {
                Plugin.LogSource.LogError($"Cannot create new music entry, Already have music with same entry name {entryName}");
                return null;
            }
            var entry = Plugin.Config.Bind(new ConfigDefinition("Basic", entryName), defaultVal, new ConfigDescription(description, tags: new ConfigurationManagerAttributes
            {
                HideDefaultButton = true,
                HideSettingName = true,
                CustomDrawer = Plugin.MusicDrawer,
            }));
            entry.SettingChanged += Plugin.Entry_SettingChanged;
            Plugin.MusicEntries[entry] = SoundData<MusicDefinition>.Create(entry, new MusicDefinition());
            return Plugin.MusicEntries[entry];
        }

        /// <inheritdoc/>
        public SoundData<SoundCueDefinition> CreateNewSFXEntry(string entryName, string description = "", string defaultVal = "")
        {
            var duplicatedEntry = Plugin.SFXEntries.Keys.FirstOrDefault(sfxEntry => sfxEntry.Definition.Key == entryName);
            if (duplicatedEntry != null)
            {
                Plugin.LogSource.LogError($"Cannot create new sfx entry, Already have SFX with same entry name {entryName}");
                return null;
            }
            var entry = Plugin.Config.Bind(new ConfigDefinition("Basic", entryName), defaultVal, new ConfigDescription(description, tags: new ConfigurationManagerAttributes
            {
                HideDefaultButton = true,
                HideSettingName = true,
                CustomDrawer = Plugin.MusicDrawer,
            }));
            entry.SettingChanged += Plugin.Entry_SettingChanged;
            Plugin.SFXEntries[entry] = SoundData<SoundCueDefinition>.Create(entry, new SoundCueDefinition());
            return Plugin.SFXEntries[entry];
        }

        /// <inheritdoc/>
        public BattleMusicTrack RegisterToBattleMusic(SoundData<MusicDefinition> musicData, bool isBoss = false)
        {
            if (musicData == null)
            {
                Plugin.LogSource.LogError("Cannot register to battle music, data is null");
                return default;
            }
            if (!HasInitializedSoundManager())
            {
                return default;
            }
            SoundManager soundManager = GetSoundManager();
            List<BattleMusicTrack> battleMusicTracks = GetBattleMusicTracks(isBoss);
            if (battleMusicTracks.Exists(track => track.trackNameData == musicData.definition.Name))
            {
                Plugin.LogSource.LogWarning($"Already registered battle music track with entry name {musicData.definition.Name}");
                Plugin.LogSource.LogWarning("Removing the duplicated entry...");
                battleMusicTracks.RemoveAll(track => track.trackNameData == musicData.entryName);
            }
            var registeredTrack = new BattleMusicTrack
            {
                publicTrackNameKey = musicData.GetTitle(),
                trackNameData = musicData.definition.Name
            };
            battleMusicTracks.Add(registeredTrack);
            RegisterMusicEntry(musicData);
            return registeredTrack;
        }

        /// <inheritdoc/>
        public void RegisterMusicEntry(SoundData<MusicDefinition> musicData)
        {
            if (musicData == null)
            {
                Plugin.LogSource.LogError("Cannot register music, data is null");
                return;
            }
            if (!HasInitializedSoundManager())
            {
                return;
            }
            SoundManager soundManager = GetSoundManager();
            CoreAudioSystem audioSystem = GetCoreAudioSystem();
            CoreAudioSystemData audioSystemData = GetCoreAudioSystemData();
            if (audioSystemData.MusicDefData.Tracks.Any(definition => definition.Name == musicData.definition.Name))
            {
                Plugin.LogSource.LogWarning($"Already registered music track with entry name {musicData.definition.Name}");
                Plugin.LogSource.LogWarning("Removing the duplicated entry...");
                var trackList = audioSystemData.MusicDefData.Tracks.ToList();
                trackList.RemoveAll(definition => definition.Name == musicData.definition.Name);
                audioSystemData.MusicDefData.Tracks = trackList.ToArray();

            }
            audioSystemData.MusicDefData.Tracks = audioSystemData.MusicDefData.Tracks.AddToArray(musicData.definition);
        }

        /// <inheritdoc/>
        public ConfigEntry<string> GetEntry(string entryName)
        {
            ConfigEntryBase resultEntry = Plugin.MusicEntries.Keys.FirstOrDefault(entry => entry.Definition.Key == entryName);
            if (resultEntry == null)
            {
                resultEntry = Plugin.SFXEntries.Keys.FirstOrDefault(entry => entry.Definition.Key == entryName);
            }
            if (resultEntry == null)
            {
                Plugin.LogSource.LogError($"Cannot find entry named {entryName}");
            }
            return resultEntry as ConfigEntry<string>;
        }

        /// <inheritdoc/>
        public bool IsMusicEntry(ConfigEntryBase entry)
        {
            return Plugin.MusicEntries.ContainsKey(entry);
        }

        /// <inheritdoc/>
        public bool IsSFXEntry(ConfigEntryBase entry)
        {
            return Plugin.SFXEntries.ContainsKey(entry);
        }

        /// <inheritdoc/>
        public bool IsValidEntry(ConfigEntryBase entry)
        {
            var isValid = entry != null && (IsMusicEntry(entry) || IsSFXEntry(entry));
            if (!isValid)
            {
                Plugin.LogSource.LogError($"{entry?.Definition?.Key} is not a valid entry");
            }
            return isValid;
        }

        /// <inheritdoc/>
        public SoundData<MusicDefinition> GetMusicData(string entryName)
        {
            var entry = GetEntry(entryName);
            return GetMusicData(entry);
        }

        /// <inheritdoc/>
        public SoundData<MusicDefinition> GetMusicData(ConfigEntryBase entry)
        {
            if (!IsValidEntry(entry))
            {
                return null;
            }
            if (!IsMusicEntry(entry))
            {
                Plugin.LogSource.LogError("Entry is valid but is not a music entry");
                return null;
            }
            return Plugin.MusicEntries[entry];
        }

        /// <inheritdoc/>
        public SoundData<SoundCueDefinition> GetSFXData(string entryName)
        {
            var entry = GetEntry(entryName);
            return GetSFXData(entry);
        }

        /// <inheritdoc/>
        public SoundData<SoundCueDefinition> GetSFXData(ConfigEntryBase entry)
        {
            if (!IsValidEntry(entry))
            {
                return null;
            }
            if (!IsSFXEntry(entry))
            {
                Plugin.LogSource.LogError("Entry is valid but is not an SFX entry");
                return null;
            }
            return Plugin.SFXEntries[entry];
        }

        /// <inheritdoc/>
        public void ChangeSound(string entryName, string filePath, bool replayMusic = true, Action<string> onPlay = null)
        {
            var entry = GetEntry(entryName);
            ChangeSound(entry, filePath, replayMusic, onPlay);
        }

        /// <inheritdoc/>
        public void ChangeSound(string entryName, AudioClip audioClip, bool replayMusic = true, Action<string> onPlay = null)
        {
            var entry = GetEntry(entryName);
            ChangeSound(entry, audioClip, replayMusic, onPlay);
        }

        /// <inheritdoc/>
        public void ChangeSound(ConfigEntryBase entry, string filePath, bool replayMusic = true, Action<string> onPlay = null)
        {
            if (!IsValidEntry(entry))
            {
                return;
            }
            if (!File.Exists(filePath))
            {
                Plugin.LogSource.LogError($"Cannot load {filePath} because its not valid path");
                return;
            }
            entry.BoxedValue = filePath;
            Plugin.Instance.StartCoroutine(Plugin.LoadAudioClip(filePath, (audioClip) =>
            {
                Plugin.OnAudioClipLoaded(entry, audioClip, replayMusic, onPlay);
            }));
        }

        /// <inheritdoc/>
        public void ChangeSound(ConfigEntryBase entry, AudioClip audioClip, bool replayMusic = true, Action<string> onPlay = null)
        {
            if (!IsValidEntry(entry))
            {
                return;
            }
            if (audioClip == null)
            {
                Plugin.LogSource.LogError($"AudioClip is null, Cannot assign to entry {entry.Definition.Key} because t");
                return;
            }
            entry.BoxedValue = audioClip.name;
            Plugin.OnAudioClipLoaded(entry, audioClip, replayMusic, onPlay);
        }

        /// <inheritdoc/>
        public void PlayEntry(string entryName, Action<string> onPlay = null)
        {
            var entry = GetEntry(entryName);
            PlayEntry(entry, onPlay: onPlay);
        }

        /// <inheritdoc/>
        public void PlayEntry(ConfigEntryBase entry, Action<string> onPlay = null)
        {
            if (!IsValidEntry(entry))
            {
                return;
            }
            if (IsMusicEntry(entry))
            {
                Plugin.PlayMusic(entry, onPlay: onPlay);
            }
            else if (IsSFXEntry(entry))
            {
                Plugin.PlaySFX(entry, onPlay: onPlay);
            }
        }

        /// <inheritdoc/>
        public void PlayMusic(string entryName, bool isBattleMusic = false, float crossfadeTimeSeconds = 0.25f, string displayedName = "", Action<string> onPlay = null)
        {
            var entry = GetEntry(entryName);
            PlayMusic(entry, isBattleMusic, crossfadeTimeSeconds, displayedName, onPlay);
        }

        /// <inheritdoc/>
        public void PlayMusic(ConfigEntryBase entry, bool isBattleMusic = false, float crossfadeTimeSeconds = 0.25f, string displayedName = "", Action<string> onPlay = null)
        {
            if (!IsValidEntry(entry))
            {
                return;
            }
            if (IsMusicEntry(entry))
            {
                Plugin.PlayMusic(entry, isBattleMusic, crossfadeTimeSeconds, displayedName, onPlay);
            }
        }

        /// <inheritdoc/>
        public void PlaySFX(string entryName, bool loop = false, Action<string> onPlay = null)
        {
            var entry = GetEntry(entryName);
            PlaySFX(entry, loop, onPlay);
        }

        /// <inheritdoc/>
        public void PlaySFX(ConfigEntryBase entry, bool loop = false, Action<string> onPlay = null)
        {
            if (!IsValidEntry(entry))
            {
                return;
            }
            if (IsSFXEntry(entry))
            {
                Plugin.PlaySFX(entry, loop, onPlay);
            }
        }

        /// <inheritdoc/>
        public void LoadFileForEntry(string entryName, Action<string, AudioClip> onLoad = null, Action<string> onPlay = null)
        {
            var entry = GetEntry(entryName);
            LoadFileForEntry(entry, onLoad, onPlay);
        }

        /// <inheritdoc/>
        public void LoadFileForEntry(ConfigEntryBase entry, Action<string, AudioClip> onLoad = null, Action<string> onPlay = null)
        {
            if (!IsValidEntry(entry))
            {
                return;
            }
            Plugin.LoadFile(entry, "", false, onLoad, onPlay);
        }

        /// <inheritdoc/>
        public void ResetEntry(string entryName)
        {
            var entry = GetEntry(entryName);
            ResetEntry(entry);
        }

        /// <inheritdoc/>
        public void ResetEntry(ConfigEntryBase entry)
        {
            if (!IsValidEntry(entry))
            {
                return;
            }
            entry.BoxedValue = "";
            Plugin.ResetEntryBase(entry);
        }

        /// <inheritdoc/>
        public string GetCurrentlyPlayingMusicName()
        {
            if (!HasInitializedSoundManager())
            {
                return "";
            }
            SoundManager soundManager = AllGameManagers.Instance.GetSoundManager();
            string musicName = soundManager.GetCurrentMusicTrackName();
            if (!string.IsNullOrWhiteSpace(soundManager.currentTrackName))
            {
                musicName = soundManager.currentTrackName;
            }
            return musicName;
        }

        /// <inheritdoc/>
        public bool HasInitializedSoundManager()
        {
            var allGameManagers = AllGameManagers.Instance;
            if (allGameManagers != null && allGameManagers.GetSoundManager() != null)
            {
                return true;
            }

            Plugin.LogSource.LogError($"{nameof(SoundManager)} isn't initialized yet, try waiting a bit more before registering");
            return false;
        }

        /// <inheritdoc/>
        public List<BattleMusicTrack> GetBattleMusicTracks(bool isBoss)
        {
            if (!HasInitializedSoundManager())
            {
                return null;
            }
            SoundManager soundManager = AllGameManagers.Instance.GetSoundManager();
            List<BattleMusicTrack> battleMusicTracks;
            if (isBoss)
            {
                battleMusicTracks = Traverse.Create(soundManager).Field<List<BattleMusicTrack>>("bossBattleMusicTracks").Value;
            }
            else
            {
                battleMusicTracks = Traverse.Create(soundManager).Field<List<BattleMusicTrack>>("battleMusicTracks").Value;
            }
            return battleMusicTracks;
        }

        /// <inheritdoc/>
        public BattleMusicTrack GetBattleMusicTrack(string trackName)
        {
            BattleMusicTrack result = default;
            if (!HasInitializedSoundManager())
            {
                return result;
            }
            List<BattleMusicTrack> battleMusicTracks = GetBattleMusicTracks(false);
            result = battleMusicTracks.Find(track => track.trackNameData == trackName);
            if (string.IsNullOrWhiteSpace(result.trackNameData))
            {
                battleMusicTracks = GetBattleMusicTracks(false);
                result = battleMusicTracks.Find(track => track.trackNameData == trackName);
            }
            return result;
        }

        /// <inheritdoc/>
        public SoundManager GetSoundManager()
        {
            if (!HasInitializedSoundManager())
            {
                return null;
            }
            return AllGameManagers.Instance.GetSoundManager();
        }

        /// <inheritdoc/>
        public CoreAudioSystem GetCoreAudioSystem()
        {
            if (!HasInitializedSoundManager())
            {
                return null;
            }
            SoundManager soundManager = AllGameManagers.Instance.GetSoundManager();
            CoreAudioSystem audioSystem = Traverse.Create(soundManager).Field<CoreAudioSystem>("audioSystem").Value;
            return audioSystem;
        }

        /// <inheritdoc/>
        public CoreAudioSystemData GetCoreAudioSystemData()
        {
            if (!HasInitializedSoundManager())
            {
                return null;
            }
            SoundManager soundManager = AllGameManagers.Instance.GetSoundManager();
            CoreAudioSystem audioSystem = Traverse.Create(soundManager).Field<CoreAudioSystem>("audioSystem").Value;
            CoreAudioSystemData audioSystemData = Traverse.Create(audioSystem).Field("AudioSystemData").GetValue<CoreAudioSystemData>();
            return audioSystemData;
        }

        /// <inheritdoc/>
        public MusicDefinition[] GetMusicDefinitions()
        {
            if (!HasInitializedSoundManager())
            {
                return null;
            }
            return GetCoreAudioSystemData().MusicDefData.Tracks;
        }

        /// <inheritdoc/>
        public SoundCueDefinition[] GetSFXDefinitions()
        {
            if (!HasInitializedSoundManager())
            {
                return null;
            }
            return GetCoreAudioSystemData().GlobalSoundEffectData.Sounds;
        }

        /// <inheritdoc/>
        public MusicDefinition GetMusicDefinition(string trackName)
        {
            if (!HasInitializedSoundManager())
            {
                return null;
            }
            return GetMusicDefinitions().First(definition => definition.Name == trackName);
        }

        /// <inheritdoc/>
        public SoundCueDefinition GetSFXDefinition(string trackName)
        {
            if (!HasInitializedSoundManager())
            {
                return null;
            }
            return GetSFXDefinitions().First(definition => definition.Name == trackName);
        }
    }
}
