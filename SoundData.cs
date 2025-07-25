using ATL;
using BepInEx.Configuration;
using ShinyShoe.Audio;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using static ShinyShoe.Audio.CoreMusicData;
using static ShinyShoe.Audio.CoreSoundEffectData;

namespace Patty_SoundChanger_MOD
{
    /// <summary>
    /// The type of the SoundData
    /// </summary>
    public enum SoundType
    {
        /// <summary>
        /// Invalid type
        /// </summary>
        Unknown,

        /// <summary>
        /// Music type
        /// </summary>
        Music,

        /// <summary>
        /// SFX type
        /// </summary>
        SFX
    }

    /// <summary>
    /// The SoundData, holds the metadata of the audio (if it exist). AudioClip, entry configuration and the definition
    /// <br/><br/>
    /// <typeparamref name="T"/> can only be <see cref="MusicDefinition"/> or <see cref="SoundCueDefinition"/>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SoundData<T> : IDisposable
    {
        internal static readonly Lazy<Track> emptyTrack = new Lazy<Track>(() => new Track());

        private AudioClip _audioData;

        /// <summary>
        /// The metadata of the file. Does not work if the metadata is stripped
        /// </summary>
        public Track TrackData { get; private set; }

        /// <summary>
        /// The AudioClip of this SoundData, make sure you know what you're doing when setting it directly.
        /// </summary>
        public AudioClip AudioData
        {
            get
            {
                return _audioData;
            }
            set
            {
                _audioData = value;
                if (type == SoundType.Music)
                {
                    var musicData = AsMusic();
                    for (int i = 0; i < musicData.definition.Clips.Length; i++)
                    {
                        musicData.definition.Clips[i].Clip = _audioData;
                    }
                }
                else if (type == SoundType.SFX)
                {
                    var sfxData = AsSFX();
                    for (int i = 0; i < sfxData.definition.Clips.Length; i++)
                    {
                        sfxData.definition.Clips[i] = _audioData;
                    }
                }
            }
        }

        /// <summary>
        /// The cover image of this file. 
        /// <br/><br/>
        /// It will look by <see cref="PictureInfo.PIC_TYPE.Front"/>. If it doesn't exist.
        /// <br/><br/>
        /// It will look by <see cref="PictureInfo.PIC_TYPE.Illustration"/>. If it doesn't exist.
        /// <br/><br/>
        /// It will look by any valid image attached on the <see cref="TrackData"/>
        /// </summary>
        public Sprite CoverSprite { get; set; }

        /// <summary>
        /// The type of this data
        /// </summary>
        public readonly SoundType type;

        /// <summary>
        /// The entry attached to this data
        /// </summary>
        public readonly ConfigEntry<string> entry;

        /// <summary>
        /// The definition attached to this data
        /// </summary>
        public readonly T definition;

        /// <summary>
        /// The entry name attached to this data.
        /// <br/><br/>
        /// This is the same as <see cref="ConfigDefinition.Key"/>
        /// </summary>
        public readonly string entryName;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        ~SoundData()
#pragma warning restore CS1591
        {
            Dispose();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.entry.SettingChanged -= Entry_SettingChanged;
        }

        private SoundData(T definition, ConfigEntry<string> entry)
        {
            this.definition = definition;
            this.entry = entry;
            entryName = entry.Definition.Key;
            TrackData = emptyTrack.Value;
            if (typeof(MusicDefinition).IsAssignableFrom(typeof(T)))
            {
                type = SoundType.Music;
            }
            else if (typeof(SoundCueDefinition).IsAssignableFrom(typeof(T)))
            {
                type = SoundType.SFX;
            }
            CreateTrackInfo();

            this.entry.SettingChanged += Entry_SettingChanged;
        }

        /// <summary>
        /// Create a new sound data attached to that entry and definition
        /// <br/><br/>
        /// Only call this if you know what you're doing
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="definition"></param>
        /// <returns></returns>
        public static SoundData<T> Create(ConfigEntry<string> entry, T definition)
        {
            if (!IsValidType())
            {
                return null;
            }
            if (entry == null)
            {
                Plugin.LogSource.LogError($"Cannot create {nameof(SoundData<T>)} with null entry");
                return null;
            }
            if (definition == null)
            {
                Plugin.LogSource.LogError($"Cannot create {nameof(SoundData<T>)} with null definition");
                return null;
            }
            return new SoundData<T>(definition, entry);
        }

        /// <summary>
        /// Check whether the type is a valid <see cref="MusicDefinition"/> or <see cref="SoundCueDefinition"/>
        /// </summary>
        /// <returns></returns>
        public static bool IsValidType()
        {
            if (typeof(MusicDefinition).IsAssignableFrom(typeof(T)) ||
                typeof(SoundCueDefinition).IsAssignableFrom(typeof(T)))
            {
                return true;
            }
            Plugin.LogSource.LogError($"T must be of type {nameof(CoreMusicData)}.{nameof(MusicDefinition)}" +
                                      $" or {nameof(CoreSoundEffectData)}.{nameof(SoundCueDefinition)}");
            return false;
        }

        private void CreateTrackInfo()
        {
            if (IsFileExist())
            {
                TrackData = new Track(GetFilePath());
                if (!TrackData.EmbeddedPictures.IsNullOrEmpty())
                {
                    PictureInfo frontPict = TrackData.EmbeddedPictures.FirstOrDefault(picture => picture?.PicType == PictureInfo.PIC_TYPE.Front);
                    if (frontPict == null)
                    {
                        frontPict = TrackData.EmbeddedPictures.FirstOrDefault(picture => picture?.PicType == PictureInfo.PIC_TYPE.Illustration);
                    }
                    if (frontPict == null)
                    {
                        frontPict = TrackData.EmbeddedPictures.FirstOrDefault(picture => picture != null);
                    }
                    if (frontPict == null)
                    {
                        return;
                    }
                    AssignCover(frontPict.PictureData);
                }
            }
            else
            {
                TrackData = emptyTrack.Value;
            }
        }

        private void Entry_SettingChanged(object sender, EventArgs e)
        {
            CreateTrackInfo();
        }

        /// <summary>
        /// Cast the sound data as a <see cref="MusicDefinition"/>
        /// </summary>
        /// <returns></returns>
        public SoundData<MusicDefinition> AsMusic()
        {
            return this as SoundData<MusicDefinition>;
        }

        /// <summary>
        /// Cast the sound data as a <see cref="SoundCueDefinition"/>
        /// </summary>
        /// <returns></returns>
        public SoundData<SoundCueDefinition> AsSFX()
        {
            return this as SoundData<SoundCueDefinition>;
        }

        /// <summary>
        /// Assign this data to that definition, very useful if you want to copy just certain parts of the definition.
        /// Or for a new entry that has empty definition
        /// </summary>
        /// <param name="definition"></param>
        /// <returns></returns>
        public bool AssignDefinitionToData(T definition)
        {
            if (!IsValidType())
            {
                return false;
            }
            if (definition == null)
            {
                Plugin.LogSource.LogError($"Cannot assign data to null definition");
                return false;
            }
            if (type == SoundType.Music)
            {
                var instanceDefinition = AsMusic();
                var targetDefinition = definition as MusicDefinition;

                if (instanceDefinition == null)
                {
                    Plugin.LogSource.LogError($"The instance isn't SoundData<MusicDefinition> somehow despite the type is a music");
                    return false;
                }
                if (targetDefinition == null)
                {
                    Plugin.LogSource.LogError($"The target instance isn't MusicDefinition somehow despite the type is a music");
                    return false;
                }
                if (instanceDefinition.definition == null)
                {
                    Plugin.LogSource.LogError($"Definition is null");
                    return false;
                }

                instanceDefinition.definition.Mixer = targetDefinition.Mixer;
                if (!targetDefinition.Clips.IsNullOrEmpty())
                {
                    instanceDefinition.definition.Clips = new MusicClipDefinition[targetDefinition.Clips.Length];
                    for (var i = 0; i < targetDefinition.Clips.Length; i++)
                    {
                        instanceDefinition.definition.Clips[i] = new MusicClipDefinition
                        {
                            MixerGroup = targetDefinition.Clips[i]?.MixerGroup,
                            Clip = targetDefinition.Clips[i]?.Clip
                        };
                    }
                }
                else
                {
                    instanceDefinition.definition.Clips = new MusicClipDefinition[1];
                }

                if (!targetDefinition.Snapshots.IsNullOrEmpty())
                {
                    instanceDefinition.definition.Snapshots = new MusicSnapshotDefinition[targetDefinition.Snapshots.Length];
                    for (var i = 0; i < targetDefinition.Clips.Length; i++)
                    {
                        instanceDefinition.definition.Snapshots[i] = new MusicSnapshotDefinition
                        {
                            Name = targetDefinition.Snapshots[i]?.Name,
                            Snapshot = targetDefinition.Snapshots[i]?.Snapshot,
                            TransitionTimeSeconds = (float)targetDefinition.Snapshots[i]?.TransitionTimeSeconds,
                        };
                    }
                }
                else
                {
                    instanceDefinition.definition.Snapshots = new MusicSnapshotDefinition[1];
                }

                instanceDefinition.definition.Name = entry.Definition.Key;
            }
            else if (type == SoundType.SFX)
            {
                var instanceDefinition = AsSFX();
                var targetDefinition = definition as SoundCueDefinition;

                if (instanceDefinition == null)
                {
                    Plugin.LogSource.LogError($"The instance isn't SoundData<SoundCueDefinition> somehow despite the type is an SFX");
                    return false;
                }
                if (targetDefinition == null)
                {
                    Plugin.LogSource.LogError($"The target instance isn't SoundCueDefinition somehow despite the type is an SFX");
                    return false;
                }
                if (instanceDefinition.definition == null)
                {
                    Plugin.LogSource.LogError($"Definition is null");
                    return false;
                }

                if (!targetDefinition.Clips.IsNullOrEmpty())
                {
                    instanceDefinition.definition.Clips = new AudioClip[targetDefinition.Clips.Length];
                    for (var i = 0; i < targetDefinition.Clips.Length; i++)
                    {
                        instanceDefinition.definition.Clips[i] = targetDefinition.Clips[i];
                    }
                }
                else
                {
                    instanceDefinition.definition.Clips = new AudioClip[1];
                }

                instanceDefinition.definition.VolumeMin = targetDefinition.VolumeMin;
                instanceDefinition.definition.VolumeMax = targetDefinition.VolumeMax;
                instanceDefinition.definition.PitchMin = targetDefinition.PitchMin;
                instanceDefinition.definition.PitchMax = targetDefinition.PitchMax;
                instanceDefinition.definition.Loop = targetDefinition.Loop;

                if (!targetDefinition.Tags.IsNullOrEmpty())
                {
                    instanceDefinition.definition.Tags = new string[targetDefinition.Tags.Length];
                    for (var i = 0; i < targetDefinition.Tags.Length; i++)
                    {
                        instanceDefinition.definition.Tags[i] = targetDefinition.Tags[i];
                    }
                }
                else
                {
                    instanceDefinition.definition.Tags = new string[1];
                }

                instanceDefinition.definition.Name = entry.Definition.Key;
            }
            return true;
        }

        /// <summary>
        /// Load an image and assign it as cover to this data
        /// </summary>
        /// <param name="filePath"></param>
        public void AssignCover(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Plugin.LogSource.LogError($"Cannot create cover for entry {entryName} on non-existent file");
                return;
            }
            AssignCover(File.ReadAllBytes(filePath));
        }

        /// <summary>
        /// Assign a cover image into this data
        /// </summary>
        /// <param name="data"></param>
        public void AssignCover(byte[] data)
        {
            var texture = new Texture2D(0, 0);
            if (texture.LoadImage(data))
            {
                CoverSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero, 100, 0, SpriteMeshType.Tight);
            }
            else
            {
                Plugin.LogSource.LogError($"Failed creating cover image for entry {entryName}");
            }
        }

        /// <summary>
        /// Get the title name. If the metadata exist, then it will get the title
        /// If it doesn't. Then it will get the file name without extension
        /// </summary>
        /// <returns></returns>
        public string GetTitle()
        {
            string result = "";
            if (!IsEmptyTrackData() && !string.IsNullOrWhiteSpace(TrackData.Title))
            {
                result = TrackData.Title;
            }
            else if (File.Exists(entry.Value))
            {
                result = Path.GetFileNameWithoutExtension(entry.Value);
            }
            else if (AudioData != null)
            {
                result = Plugin.RemoveGUIDFromName(AudioData);
            }
            return result;
        }

        /// <summary>
        /// Get the file path of this data
        /// </summary>
        /// <returns></returns>
        public string GetFilePath()
        {
            string result = entry.Value;
            if (!File.Exists(result))
            {
                if (Path.IsPathRooted(result))
                {
                    Plugin.LogSource.LogError($"The path must not be rooted if you want to make it relative to the mod path {result}");
                }
                result = Path.Combine(Plugin.BasePath, result);
            }
            return result;
        }

        /// <summary>
        /// Get the file extension of this data (including the period)
        /// </summary>
        /// <returns></returns>
        public string GetExtension()
        {
            return Path.GetExtension(entry.Value);
        }

        /// <summary>
        /// Check whether the file exist for this data
        /// </summary>
        /// <returns></returns>
        public bool IsFileExist()
        {
            return File.Exists(GetFilePath());
        }

        /// <summary>
        /// Check whether the metadata is invalid for this data
        /// </summary>
        /// <returns></returns>
        public bool IsEmptyTrackData()
        {
            return TrackData == emptyTrack.Value;
        }

        /// <summary>
        /// Check the duration of this data
        /// </summary>
        /// <returns></returns>
        public int Duration()
        {
            int duration = 0;
            if (AudioData != null)
            {
                duration = Mathf.RoundToInt(AudioData.length);
            }
            else if (!IsEmptyTrackData())
            {
                duration = TrackData.Duration;
            }
            return duration;
        }
    }
}
