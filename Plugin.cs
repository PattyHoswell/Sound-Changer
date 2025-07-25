using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using I2.Loc;
using ShinyShoe;
using ShinyShoe.Audio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.U2D;
using static ShinyShoe.Audio.CoreMusicData;
using static ShinyShoe.Audio.CoreSoundEffectData;
using static SoundManager;

namespace Patty_SoundChanger_MOD
{
    /// <summary>
    /// The main class of this mod
    /// </summary>
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// A constant string for the section
        /// </summary>
        public const string MUSIC_SECTION = "Music", SFX_SECTION = "SFX";

        internal static bool Initialized { get; private set; }
        internal static string BasePath { get; } = Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName;
        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource LogSource { get; private set; }
        internal static Harmony PluginHarmony { get; private set; }
        internal static new ConfigFile Config { get; private set; }
        internal static OrderedDictionary<ConfigEntryBase, SoundData<MusicDefinition>> MusicEntries { get; private set; } =
                    new OrderedDictionary<ConfigEntryBase, SoundData<MusicDefinition>>();
        internal static OrderedDictionary<ConfigEntryBase, SoundData<SoundCueDefinition>> SFXEntries { get; private set; } =
                    new OrderedDictionary<ConfigEntryBase, SoundData<SoundCueDefinition>>();
        internal static OrderedDictionary<MusicDefinition, List<AudioClip>> OriginalMusics { get; private set; } =
                    new OrderedDictionary<MusicDefinition, List<AudioClip>>();
        internal static OrderedDictionary<SoundCueDefinition, List<AudioClip>> OriginalSFX { get; private set; } =
                    new OrderedDictionary<SoundCueDefinition, List<AudioClip>>();
        internal static Traverse configurationManagerTraverse;
        internal static Lazy<GUIStyle> centeredStyle = new Lazy<GUIStyle>(() =>
        {
            var style = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter
            };
            return style;
        });
        internal static int LeftColumnWidth
        {
            get
            {
                if (configurationManagerTraverse == null)
                {
                    return 260;
                }
                var property = configurationManagerTraverse.Property("LeftColumnWidth");
                if (!property.PropertyExists())
                {
                    return 260;
                }
                return property.GetValue<int>();
            }
        }
        internal static ConfigEntry<string> currentSectionEntry;
        internal static MethodInfo rebuildSettings;
        internal static SpriteAtlas scrollbarAtlas;
        internal static ConfigEntry<bool> enableIngameMenu;

        /// <summary>
        /// Triggers when the music changed in game
        /// </summary>
        public static readonly Signal<string> musicChanged = new Signal<string>();

        /// <summary>
        /// The manager of this mod for your mod to use
        /// </summary>
        public static readonly ISoundChanger soundChangerManager = new SoundChangerManager();

        /// <summary>
        /// Triggers when this mod is initialized
        /// </summary>
        public static readonly Signal onInitialize = new Signal();

        void Awake()
        {
            Instance = this;
            LogSource = Logger;
            Config = base.Config;
            try
            {
                PluginHarmony = Harmony.CreateAndPatchAll(typeof(PatchList), PluginInfo.GUID);
            }
            catch (HarmonyException ex)
            {
                LogSource.LogError((ex.InnerException ?? ex).Message);
            }
            try
            {
                PluginHarmony.PatchAll(typeof(TranspilerFix));
            }
            catch (HarmonyException ex)
            {
                // If there's an error we'll assume the developer already fixed it, but if they don't, then apply another fix
                LogSource.LogError((ex.InnerException ?? ex).Message);
                try
                {
                    PluginHarmony.PatchAll(typeof(TemporaryFix));
                }
                catch (HarmonyException ex2)
                {
                    LogSource.LogError((ex2.InnerException ?? ex2).Message);
                }
            }

            // Don't assume the user has the plugin installed, only obtain if its installed
            if (Chainloader.PluginInfos.TryGetValue("com.bepis.bepinex.configurationmanager", out var pluginInfo))
            {
                configurationManagerTraverse = Traverse.Create(pluginInfo.Instance);
                try
                {
                    var targetType = pluginInfo.Instance.GetType().Assembly.GetType("ConfigurationManager.ConfigurationManager");
                    rebuildSettings = AccessTools.DeclaredMethod(targetType, "BuildSettingList");
                }
                catch (Exception ex)
                {
                    LogSource.LogError((ex.InnerException ?? ex).Message);
                }
            }

            enableIngameMenu = Config.Bind(new ConfigDefinition("Basic", "Toggle in-game menu"), true,
            new ConfigDescription("If enabled, then create an in-game menu (recommended to disable if future game update breaks this mod). Restart to apply", tags: new ConfigurationManagerAttributes
            {
                Order = 10001
            }));

            if (enableIngameMenu.Value)
            {
                try
                {
                    PluginHarmony.PatchAll(typeof(MenuPatch));
                }
                catch (HarmonyException ex)
                {
                    LogSource.LogError((ex.InnerException ?? ex).Message);
                }
            }

            Config.Bind<string>(new ConfigDefinition("Basic", "Currently Playing"), "",
            new ConfigDescription("Only for references as to what music is currently playing in game", tags: new ConfigurationManagerAttributes
            {
                Order = 10000,
                CustomDrawer = CurrentlyPlayingDrawer,
                HideDefaultButton = true,
                HideSettingName = true,
                ReadOnly = true,
            }));

            currentSectionEntry = Config.Bind<string>(new ConfigDefinition("Basic", "Toggled Section"), "",
            new ConfigDescription("Only for toggling sections", tags: new ConfigurationManagerAttributes
            {
                Order = 9999,
                CustomDrawer = ToggleSectionDrawer,
                HideDefaultButton = true,
                HideSettingName = true,
                ReadOnly = true,
            }));

            if (string.IsNullOrEmpty(currentSectionEntry.Value) ||
                (currentSectionEntry.Value != MUSIC_SECTION &&
                currentSectionEntry.Value != SFX_SECTION))
            {
                currentSectionEntry.Value = MUSIC_SECTION;
            }

            if (enableIngameMenu.Value)
            {
                var assetBundle = AssetBundle.LoadFromFile(Path.Combine(BasePath, "scrollbar.bundle"));
                scrollbarAtlas = assetBundle.LoadAsset<SpriteAtlas>("ScrollbarAtlas");
                assetBundle.Unload(unloadAllLoadedObjects: false);
            }

            Plugin.musicChanged.AddListener(OnMusicChanged);
        }

        void OnDestroy()
        {
            Plugin.musicChanged.RemoveListener(OnMusicChanged);
        }

        internal static void OnMusicChanged(string trackName)
        {
            var soundManager = AllGameManagers.Instance.GetSoundManager();
            Traverse.Create(soundManager).Property("currentTrackName").SetValue(trackName);
        }

        internal static void CreateEntries()
        {
            if (Initialized)
            {
                return;
            }
            var allGameManager = AllGameManagers.Instance;
            if (allGameManager == null)
            {
                return;
            }
            if (allGameManager.GetSoundManager() == null)
            {
                return;
            }
            Initialized = true;
            if (enableIngameMenu.Value)
            {
                var settingManager = (SettingsScreen)allGameManager.GetScreenManager().GetScreen(ScreenName.Settings);
                SoundDialog.CreateDialog(settingManager);
            }
            var soundManager = allGameManager.GetSoundManager();
            var soundManagerTraverse = Traverse.Create(soundManager);
            var coreAudio = soundManagerTraverse.Field<CoreAudioSystem>("audioSystem").Value;
            var audioSystemData = Traverse.Create(coreAudio).Field("AudioSystemData").GetValue<CoreAudioSystemData>();
            foreach (CoreMusicData.MusicDefinition definition in audioSystemData.MusicDefData.Tracks.OrderBy(track => track.Name))
            {
                CreateEntry(definition);
            }
            foreach (CoreSoundEffectData.SoundCueDefinition sfxDefinition in audioSystemData.GlobalSoundEffectData.Sounds.OrderBy(track => track.Name))
            {
                CreateEntry(sfxDefinition);
            }
            SetBrowsableSection(currentSectionEntry.Value);
            onInitialize.Dispatch();
        }

        internal static void CreateEntry(CoreMusicData.MusicDefinition definition)
        {
            if (definition == null)
            {
                return;
            }
            if (MusicEntries.Values.Any(data => data?.definition == definition))
            {
                return;
            }
            if (!OriginalMusics.ContainsKey(definition))
            {
                OriginalMusics[definition] = new List<AudioClip>();
                foreach (var musicDefinition in definition.Clips)
                {
                    OriginalMusics[definition].Add(musicDefinition.Clip);
                }
            }
            var entry = Config.Bind(new ConfigDefinition("Basic", definition.Name), "", new ConfigDescription("", tags: new ConfigurationManagerAttributes
            {
                HideDefaultButton = true,
                HideSettingName = true,
                CustomDrawer = MusicDrawer,
            }));
            entry.SettingChanged += Entry_SettingChanged;
            MusicEntries[entry] = SoundData<MusicDefinition>.Create(entry, definition);
            if (MusicEntries[entry].IsFileExist())
            {
                Instance.StartCoroutine(LoadAudioClip(MusicEntries[entry].GetFilePath(), (audioClip) =>
                {
                    OnAudioClipLoaded(entry, audioClip);
                }));
            }
        }

        internal static void CreateEntry(CoreSoundEffectData.SoundCueDefinition definition)
        {
            if (definition == null)
            {
                return;
            }
            if (SFXEntries.Values.Any(data => data?.definition == definition))
            {
                return;
            }
            if (!OriginalSFX.ContainsKey(definition))
            {
                OriginalSFX[definition] = new List<AudioClip>();
                foreach (var musicDefinition in definition.Clips)
                {
                    OriginalSFX[definition].Add(musicDefinition);
                }
            }
            var entry = Config.Bind(new ConfigDefinition("Basic", definition.Name), "", new ConfigDescription("", tags: new ConfigurationManagerAttributes
            {
                HideDefaultButton = true,
                HideSettingName = true,
                CustomDrawer = MusicDrawer,
            }));
            entry.SettingChanged += Entry_SettingChanged;
            SFXEntries[entry] = SoundData<SoundCueDefinition>.Create(entry, definition);
            if (SFXEntries[entry].IsFileExist())
            {
                Instance.StartCoroutine(LoadAudioClip(SFXEntries[entry].GetFilePath(), (audioClip) =>
                {
                    OnAudioClipLoaded(entry, audioClip);
                }));
            }
        }

        internal static void Entry_SettingChanged(object sender, EventArgs e)
        {
            ResetEntryBase(((SettingChangedEventArgs)e).ChangedSetting);
        }

        internal static void ResetEntryBase(ConfigEntryBase entryBase, Action<string> onPlay = null)
        {
            var strValue = entryBase.BoxedValue as string;
            if (string.IsNullOrEmpty(strValue) ||
                !File.Exists(strValue))
            {
                var allGameManager = AllGameManagers.Instance;
                if (allGameManager == null)
                {
                    return;
                }
                if (allGameManager.GetSoundManager() == null)
                {
                    return;
                }
                var soundManager = allGameManager.GetSoundManager();
                if (MusicEntries.TryGetValue(entryBase, out SoundData<MusicDefinition> musicData))
                {
                    string musicName = soundManager.GetCurrentMusicTrackName();
                    foreach (var originalMusic in OriginalMusics)
                    {
                        if (originalMusic.Key == musicData.definition)
                        {
                            var clips = originalMusic.Key.Clips;
                            for (var i = 0; i < clips.Length; i++)
                            {
                                clips[i].Clip = originalMusic.Value[i];
                            }
                            break;
                        }
                    }
                    if (musicData.entryName == musicName)
                    {
                        ReplayMusic(entryBase, GetPlayingMusicName(), onPlay);
                    }
                }
                else if (SFXEntries.TryGetValue(entryBase, out SoundData<SoundCueDefinition> sfxData))
                {
                    foreach (var originalSFX in OriginalSFX)
                    {
                        if (originalSFX.Key == sfxData.definition)
                        {
                            var clips = originalSFX.Key.Clips;
                            for (var i = 0; i < clips.Length; i++)
                            {
                                clips[i] = originalSFX.Value[i];
                            }
                            break;
                        }
                    }
                }
            }
        }

        internal static string GetPlayingMusicName()
        {
            var allGameManager = AllGameManagers.Instance;
            if (allGameManager == null)
            {
                return "";
            }
            if (allGameManager.GetSoundManager() == null)
            {
                return "";
            }
            var soundManager = allGameManager.GetSoundManager();
            var soundManagerTraverse = Traverse.Create(soundManager);
            var battleMusicTracks = soundManagerTraverse.Field<List<BattleMusicTrack>>("battleMusicTracks").Value;
            var bossBattleMusicTracks = soundManagerTraverse.Field<List<BattleMusicTrack>>("bossBattleMusicTracks").Value;
            battleMusicTracks.AddRange(bossBattleMusicTracks);

            var displayName = "";
            var musicName = soundManager.GetCurrentMusicTrackName();
            var battleTrack = battleMusicTracks.Find(track => track.trackNameData == musicName);
            if (!string.IsNullOrEmpty(battleTrack.publicTrackNameKey))
            {
                displayName = battleTrack.publicTrackNameKey;
                if (LocalizationManager.IsTranslatableTerm(displayName))
                {
                    displayName = displayName.Localize();
                }
            }
            return displayName;
        }

        internal static string GetModifiedAudioName(string trackName, CoreAudioSystem coreAudioSystem)
        {
            if (string.IsNullOrWhiteSpace(trackName))
            {
                return "";
            }
            var getCurrentTrack = Traverse.Create(coreAudioSystem).Method("GetCurrentTrack");
            if (!getCurrentTrack.MethodExists())
            {
                return "";
            }
            var musicDefinitionTraverse = Traverse.Create(getCurrentTrack.GetValue()).Field("MusicDefinition");
            if (!musicDefinitionTraverse.FieldExists() ||
                musicDefinitionTraverse.GetValue<MusicDefinition>() == null)
            {
                return "";
            }
            var musicDefinition = musicDefinitionTraverse.GetValue<MusicDefinition>();
            if (musicDefinition == null || musicDefinition.Clips.IsNullOrEmpty())
            {
                return "";
            }
            if (musicDefinition.Name != trackName)
            {
                if (!trackName.StartsWith("KEY>>") || !trackName.EndsWith("<<"))
                {
                    return "";
                }
                // Check if the trackName is being wrapped "KEY>>{trackName}<<"
                trackName = trackName.Substring("KEY>>".Length, trackName.Length - ("KEY>>".Length + "<<".Length));
                if (musicDefinition.Name != trackName)
                {
                    return "";
                }
            }
            var musicClipDefinition = musicDefinition.Clips.FirstOrDefault(source =>
                                                                           source != null &&
                                                                           source.Clip != null &&
                                                                           source.Clip.name.Contains(PluginInfo.GUID));
            if (musicClipDefinition != null)
            {
                return RemoveGUIDFromName(musicClipDefinition.Clip);
            }
            return "";
        }

        internal static string RemoveGUIDFromName(AudioClip audioClip)
        {
            /*
             * The AudioClip name format is GUID_FileName
             * We are removing the GUID_
             */
            var removeLength = PluginInfo.GUID.Length + 1;
            return audioClip.name.Substring(removeLength, audioClip.name.Length - removeLength);
        }

        internal static void ToggleSectionDrawer(ConfigEntryBase entry)
        {
            var pressedMusic = GUILayout.Button($"Toggle {MUSIC_SECTION}", GUILayout.ExpandWidth(true));
            var pressedSFX = GUILayout.Button($"Toggle {SFX_SECTION}", GUILayout.ExpandWidth(true));
            if (pressedMusic)
            {
                currentSectionEntry.Value = MUSIC_SECTION;
                SetBrowsableSection(currentSectionEntry.Value);
            }
            else if (pressedSFX)
            {
                currentSectionEntry.Value = SFX_SECTION;
                SetBrowsableSection(currentSectionEntry.Value);
            }
        }

        internal static void SetBrowsableSection(string section, bool updateList = true)
        {
            var showMusic = section == Plugin.MUSIC_SECTION;
            var showSFX = section == Plugin.SFX_SECTION;

            SetEntriesBrowsable(SFXEntries.Keys, showSFX);
            SetEntriesBrowsable(MusicEntries.Keys, showMusic);

            if (!updateList || rebuildSettings == null)
            {
                return;
            }
            rebuildSettings.Invoke(configurationManagerTraverse.GetValue(), null);
        }

        internal static void SetEntriesBrowsable(IEnumerable<object> entries, bool browsable)
        {
            foreach (var entry in entries)
            {
                var configEntry = (ConfigEntry<string>)entry;
                var managerAttr = (ConfigurationManagerAttributes)configEntry.Description.Tags.First();
                managerAttr.Browsable = browsable;
            }
        }

        internal static void CurrentlyPlayingDrawer(ConfigEntryBase entry)
        {
            var allGameManager = AllGameManagers.Instance;
            if (allGameManager == null)
            {
                return;
            }
            if (allGameManager.GetSoundManager() == null)
            {
                return;
            }
            SoundManager soundManager = allGameManager.GetSoundManager();
            string musicName = soundManager.GetCurrentMusicTrackName();
            if (!string.IsNullOrEmpty(soundManager.currentTrackName))
            {
                musicName = soundManager.currentTrackName;
            }
            GUILayout.Label($"<color=white>Currently playing:</color> <color=yellow>{musicName}</color>", centeredStyle.Value, GUILayout.ExpandWidth(true));
        }

        internal static bool IsCurrentMusic(ConfigEntryBase entry)
        {
            var isCurrentMusic = false;
            var allGameManager = AllGameManagers.Instance;
            if (allGameManager != null &&
                allGameManager.GetSoundManager() != null &&
                allGameManager.GetSoundManager().GetCurrentMusicTrackName() == entry.Definition.Key)
            {
                isCurrentMusic = true;
            }
            return isCurrentMusic;
        }

        internal static void MusicDrawer(ConfigEntryBase entry)
        {
            var isCurrentMusic = IsCurrentMusic(entry);
            var startTag = "";
            var endTag = "";
            if (isCurrentMusic)
            {
                startTag = "<color=yellow>";
                endTag = "</color>";
            }
            GUILayout.Label(new GUIContent($"{startTag}{entry.Definition.Key}{endTag}", null, entry.Description.Description), new GUILayoutOption[]
            {
                GUILayout.Width((float)LeftColumnWidth - 20),
                GUILayout.MaxWidth((float)LeftColumnWidth - 20)
            });
            var targetPath = entry.BoxedValue as string;
            var existFile = File.Exists(targetPath);
            if (targetPath != null && existFile)
            {
                GUILayout.Label($"{startTag}{Path.GetFileName(targetPath)}{endTag}", GUILayout.ExpandWidth(true));
            }
            var pressed = GUILayout.Button("Load File", GUILayout.ExpandWidth(true));
            if (GUILayout.Button(" ▶ ", GUILayout.ExpandWidth(false)))
            {
                if (MusicEntries.ContainsKey(entry))
                {
                    PlayMusic(entry);
                }
                else
                {
                    PlaySFX(entry);
                }
            }
            if (existFile)
            {
                if (GUILayout.Button("Reset", GUILayout.ExpandWidth(false)))
                {
                    entry.BoxedValue = "";
                    ResetEntryBase(entry);
                }
            }
            if (pressed)
            {
                LoadFile(entry, targetPath, existFile);
            }
        }

        internal static void LoadFile(ConfigEntryBase entry,
                                      string targetPath,
                                      bool existFile,
                                      Action<string, AudioClip> onComplete = null,
                                      Action<string> onPlay = null)
        {
            var filter = "Supported Files (*.wav; *.ogg; *.mp3)\0*.wav;*.ogg;*.mp3\0" +
                         "WAV Files (*.wav)\0*.wav\0" +
                         "OGG Files (*.ogg)\0*.ogg\0" +
                         "MP3 Files (*.mp3)\0*.mp3\0" +
                         "All Files\0*.*\0\0";
            var initialDirectory = BasePath;
            if (existFile)
            {
                initialDirectory = Directory.GetParent(targetPath).FullName;
            }
            NativeFileDialog.OpenSingleFile((filePath) =>
            {
                Instance.StartCoroutine(LoadAudioClip(filePath, (audioClip) =>
                {
                    if (audioClip == null)
                    {
                        return;
                    }
                    entry.BoxedValue = filePath;
                    OnAudioClipLoaded(entry, audioClip, true, onPlay);
                    onComplete?.Invoke(filePath, audioClip);
                }));
            }, "Select audio to replace with", filter, initialDirectory, targetPath);
        }


        internal static AudioType GetAudioType(string filePath)
        {
            AudioType result;
            switch (Path.GetExtension(filePath)?.ToUpperInvariant())
            {
                case ".MP3":
                    result = AudioType.MPEG;
                    break;
                case ".OGG":
                    result = AudioType.OGGVORBIS;
                    break;
                case ".WAV":
                    result = AudioType.WAV;
                    break;
                default:
                    result = AudioType.UNKNOWN;
                    break;
            }
            return result;
        }

        internal static void OnAudioClipLoaded(ConfigEntryBase entry,
                                               AudioClip audioClip,
                                               bool replayMusic = false,
                                               Action<string> onPlay = null)
        {
            try
            {
                if (audioClip == null)
                {
                    return;
                }
                if (MusicEntries.TryGetValue(entry, out SoundData<MusicDefinition> musicData))
                {
                    musicData.AudioData = audioClip;
                    if (musicData.definition?.Clips?.IsNullOrEmpty() == true)
                    {
                        LogSource.LogError($"Definition is null, you're likely calling load before assigning a definition after creating an empty SoundData. " +
                                           $"Try to use {nameof(SoundData<MusicDefinition>.AssignDefinitionToData)} before loading a file");
                        return;
                    }
                    if (!replayMusic)
                    {
                        return;
                    }
                    ReplayMusic(entry, onPlay: onPlay);
                }
                else if (SFXEntries.TryGetValue(entry, out SoundData<SoundCueDefinition> sfxData))
                {
                    sfxData.AudioData = audioClip;
                    if (sfxData.definition?.Clips?.IsNullOrEmpty() == true)
                    {
                        LogSource.LogError($"Definition is null, you're likely calling load before assigning a definition after creating an empty SoundData. " +
                                           $"Try to use {nameof(SoundData<SoundCueDefinition>.AssignDefinitionToData)} before loading a file");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                LogSource.LogError((ex.InnerException ?? ex).Message);
                LogSource.LogError($"You're likely loading too early before the Definition has been loaded, connect a method to {nameof(Plugin)}.{nameof(onInitialize)} instead");
            }
        }

        internal static void ReplayMusic(ConfigEntryBase entry,
                                         string displayedName = "",
                                         Action<string> onPlay = null)
        {
            var allGameManager = AllGameManagers.Instance;
            if (allGameManager == null)
            {
                return;
            }
            if (allGameManager.GetSoundManager() == null)
            {
                return;
            }
            var soundManager = allGameManager.GetSoundManager();
            if (soundManager.GetCurrentMusicTrackName() == entry.Definition.Key)
            {
                PlayMusic(entry, onPlay: onPlay);
            }
        }

        internal static void PlayMusic(ConfigEntryBase entry,
                                       bool isBattleMusic = false,
                                       float crossfadeTimeSeconds = 0.25f,
                                       string displayedName = "",
                                       Action<string> onPlay = null)
        {
            var allGameManager = AllGameManagers.Instance;
            if (allGameManager == null)
            {
                return;
            }
            if (allGameManager.GetSoundManager() == null)
            {
                return;
            }
            var soundManager = allGameManager.GetSoundManager();
            soundManager.StopMusic();
            IEnumerator WaitUntilMusicStops()
            {
                while (!string.IsNullOrWhiteSpace(soundManager.GetCurrentMusicTrackName()))
                {
                    yield return null;
                }
                if (isBattleMusic)
                {
                    soundManager.PlayBattleMusic(crossfadeTimeSeconds, entry.Definition.Key);
                }
                else
                {
                    soundManager.PlayMusic(entry.Definition.Key, crossfadeTimeSeconds);
                }
                if (!string.IsNullOrEmpty(displayedName))
                {
                    var currentTrackName = Traverse.Create(soundManager).Property("currentTrackName");
                    currentTrackName.SetValue(displayedName);
                }
                onPlay?.Invoke(soundManager.currentTrackName);
            }
            Instance.StartCoroutine(WaitUntilMusicStops());
        }

        internal static void PlaySFX(ConfigEntryBase entry, bool loop = false, Action<string> onPlay = null)
        {
            var allGameManager = AllGameManagers.Instance;
            if (allGameManager == null)
            {
                return;
            }
            if (allGameManager.GetSoundManager() == null)
            {
                return;
            }
            var soundManager = allGameManager.GetSoundManager();
            var sfxEntry = SFXEntries[entry];
            var looping = sfxEntry.definition.Loop;
            sfxEntry.definition.Loop = loop;
            soundManager.PlaySfx(entry.Definition.Key);
            sfxEntry.definition.Loop = looping;
            onPlay?.Invoke(entry.Definition.Key);
        }

        internal static IEnumerator LoadAudioClip(string filePath, Action<AudioClip> onComplete = null)
        {
            using (UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip(filePath, GetAudioType(filePath)))
            {
                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    var result = DownloadHandlerAudioClip.GetContent(webRequest);
                    result.name = $"{PluginInfo.GUID}_{Path.GetFileNameWithoutExtension(filePath)}";
                    onComplete?.Invoke(result);
                }
                else
                {
                    LogSource.LogError(webRequest.error);
                    onComplete?.Invoke(null);
                }
            }
        }
    }
}
