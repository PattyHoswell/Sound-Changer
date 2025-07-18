using BepInEx.Configuration;
using HarmonyLib;
using I2.Loc;
using ShinyShoe;
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Patty_SoundChanger_MOD
{
    internal class SoundInfo : MonoBehaviour
    {
        internal ConfigEntryBase entry;
        internal TMP_Text titleTMP, replacementTMP;
        internal Dictionary<GameUISelectableButton, Action> buttons = new Dictionary<GameUISelectableButton, Action>(3);

        internal bool TryTrigger(CoreInputControlMapping mapping, IGameUIComponent triggeredUI, InputManager.Controls triggeredMappingID)
        {
            foreach (var pair in buttons)
            {
                if (pair.Key.TryTrigger(mapping, triggeredUI, triggeredMappingID))
                {
                    pair.Value.Invoke();
                    return true;
                }
            }
            return false;
        }

        internal void Set(SettingsScreen settingsScreen, ConfigEntryBase entry)
        {
            var settingTraverse = Traverse.Create(settingsScreen);
            var pauseDialog = settingTraverse.Field<PauseDialog>("pauseDialog").Value;
            var settingButton = Traverse.Create(pauseDialog).Field<GameUISelectableButton>("settingsButton").Value;
            var musicLabel = Traverse.Create(pauseDialog).Field<TMP_Text>("musicLabel").Value;

            var gridLayout = gameObject.AddComponent<ContentSizeFitter>();
            gridLayout.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            gridLayout.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = Instantiate(musicLabel, gridLayout.transform);
            DestroyImmediate(title.GetComponent<Localize>());
            title.rectTransform.anchorMin = Vector2.zero;
            title.rectTransform.pivot = new Vector2(0, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(0, 0);
            title.rectTransform.sizeDelta = new Vector2(300, 33);
            title.name = "Label";
            title.alignment = TextAlignmentOptions.Left;
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.fontSizeMax = 30;
            title.fontSizeMin = 18;
            title.enableAutoSizing = true;

            var customMusicTitle = Instantiate(title, gridLayout.transform);
            customMusicTitle.rectTransform.anchoredPosition = new Vector2(320, 0);

            var resetButton = Instantiate(settingButton, gridLayout.transform);
            resetButton.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0.5f);
            resetButton.GetComponent<RectTransform>().anchorMin = new Vector2(1, 0.5f);
            resetButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-20, 0);
            resetButton.GetComponent<RectTransform>().pivot = new Vector2(1, 0.5f);
            resetButton.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 64);
            DestroyImmediate(resetButton.GetComponentInChildren<Localize>());
            resetButton.GetComponentInChildren<TextMeshProUGUI>().text = "Reset";
            resetButton.name = "Reset Button";

            var loadFileButton = Instantiate(resetButton, gridLayout.transform);
            loadFileButton.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 64);
            loadFileButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-150, 0);
            loadFileButton.GetComponentInChildren<TextMeshProUGUI>().text = "Load File";
            loadFileButton.name = "Load File Button";

            var playButton = Instantiate(loadFileButton, gridLayout.transform);
            playButton.GetComponent<RectTransform>().sizeDelta = new Vector2(50, 64);
            playButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-300, 0);
            playButton.GetComponentInChildren<TextMeshProUGUI>().text = " ▶ ";
            playButton.name = "Load File Button";

            this.entry = entry;
            ((ConfigEntry<string>)this.entry).SettingChanged += SoundInfo_SettingChanged;
            titleTMP = title;
            replacementTMP = customMusicTitle;
            buttons[playButton] = PlayMusic;
            buttons[loadFileButton] = LoadFile;
            buttons[resetButton] = ResetMusic;

            UpdateText();
            Plugin.musicChanged.AddListener(OnMusicChanged);
        }

        void OnDestroy()
        {
            ((ConfigEntry<string>)entry).SettingChanged -= SoundInfo_SettingChanged;
            Plugin.musicChanged.RemoveListener(OnMusicChanged);
        }

        void OnMusicChanged(string trackName)
        {
            UpdateText();
        }

        private void SoundInfo_SettingChanged(object sender, System.EventArgs e)
        {
            UpdateText();
        }

        internal void PlayMusic()
        {
            if (Plugin.MusicEntries.ContainsKey(entry))
            {
                Plugin.PlayMusic(entry, onPlay: (trackName) =>
                {
                    UpdateText();
                });
            }
            else
            {
                Plugin.PlaySFX(entry);
            }
        }

        internal void LoadFile()
        {
            var targetPath = entry.BoxedValue as string;
            var existFile = File.Exists(targetPath);
            Plugin.LoadFile(entry, targetPath, existFile, onPlay: (trackName) =>
            {
                UpdateText();
                SoundDialog.Instance.UpdateCurrentMusicName(trackName);
            });
        }

        internal void ResetMusic()
        {
            entry.BoxedValue = "";
            Plugin.ResetEntryBase(entry, onPlay: (trackName) =>
            {
                UpdateText();
            });
        }

        internal void UpdateText()
        {
            var isCurrentMusic = Plugin.IsCurrentMusic(entry);
            var startTag = "";
            var endTag = "";
            if (isCurrentMusic)
            {
                startTag = "<color=yellow>";
                endTag = "</color>";
            }
            titleTMP.text = $"{startTag}{entry.Definition.Key}{endTag}";
            replacementTMP.text = $"{startTag}{Path.GetFileName(entry.BoxedValue as string)}{endTag}";
        }
    }
}
