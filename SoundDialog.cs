using BepInEx.Configuration;
using HarmonyLib;
using I2.Loc;
using ShinyShoe;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Patty_SoundChanger_MOD
{
    /// <summary>
    /// The main class for in-game SoundDialog
    /// </summary>
    public class SoundDialog : ScreenDialog
    {
        /// <summary>
        /// The instance the mod has created
        /// </summary>
        public static SoundDialog Instance { get; private set; }

        /// <summary>
        /// Attached settings screen
        /// </summary>
        public static SettingsScreen SettingsScreen { get; } = (SettingsScreen)AllGameManagers.Instance.GetScreenManager().GetScreen(ScreenName.Settings);

        /// <summary>
        /// Attached pause dialog
        /// </summary>
        public static PauseDialog PauseDialog { get; private set; }

        internal Dictionary<string, ScrollRect> scrollRectsSection = new Dictionary<string, ScrollRect>();
        internal Dictionary<string, SettingsTab> tabsSection = new Dictionary<string, SettingsTab>();
        internal List<SoundInfo> soundList = new List<SoundInfo>();
        internal GameUISelectableButton soundChangerButton;
        internal TextMeshProUGUI headerTitle;
        internal TMP_Text musicLabel;
        internal LayoutElement ContentLayout;
        void Start()
        {
            name = nameof(SoundDialog);
            PauseDialog = Traverse.Create(SettingsScreen).Field<PauseDialog>("pauseDialog").Value;

            headerTitle = transform.Find("Header/Title").GetComponent<TextMeshProUGUI>();
            headerTitle.enableAutoSizing = true;
            headerTitle.overflowMode = TextOverflowModes.Ellipsis;
            headerTitle.fontSizeMin = 20;
            headerTitle.rectTransform.sizeDelta = new Vector2(-100, 64);

            musicLabel = Traverse.Create(PauseDialog).Field<TMP_Text>("musicLabel").Value;
            musicLabel.overflowMode = TextOverflowModes.Ellipsis;

            ContentLayout = new GameObject("Content Layout").AddComponent<LayoutElement>();
            ContentLayout.preferredHeight = 818;
            ContentLayout.transform.SetParent(transform.Find("Content"), false);

            InitializeTabs();
            CreateSection(Plugin.MUSIC_SECTION, tabsSection[Plugin.MUSIC_SECTION], Plugin.MusicEntries.Keys.ToList());
            CreateSection(Plugin.SFX_SECTION, tabsSection[Plugin.SFX_SECTION], Plugin.SFXEntries.Keys.ToList());

            var screenTraverse = Traverse.Create(this);
            _canvasGroup = GetComponent<CanvasGroup>();
            screenTraverse.Field("_timelineTransition").SetValue(GetComponent<TimelineTransition>());
            _closeButton = transform.Find("Bg container/CloseButton").GetComponent<GameUISelectableButton>();

            Traverse.Create(PauseDialog).Field<GameObject>("musicLabelRoot").Value.SetActive(true);
            Plugin.musicChanged.AddListener(UpdateCurrentMusicName);

            SetSection(Plugin.currentSectionEntry.Value, false);

            Plugin.currentSectionEntry.SettingChanged += CurrentSectionEntry_SettingChanged;
            SoundDialog.Instance.UpdateCurrentMusicName(AllGameManagers.Instance.GetSoundManager().currentTrackName);
        }

        private void CurrentSectionEntry_SettingChanged(object sender, System.EventArgs e)
        {
            SetSection(Plugin.currentSectionEntry.Value, false);
        }

        private void CreateSoundChangerButton(PauseDialog pauseDialog)
        {
            var settingButton = Traverse.Create(pauseDialog).Field<GameUISelectableButton>("settingsButton").Value;
            soundChangerButton = Instantiate(settingButton, settingButton.transform.parent);
            soundChangerButton.transform.SetSiblingIndex(1);
            var soundChangerTMP = soundChangerButton.GetComponentInChildren<TMP_Text>(true);
            DestroyImmediate(soundChangerTMP.GetComponent<Localize>());
            soundChangerTMP.text = "Sound Changer";
        }

        void OnDestroy()
        {
            Plugin.musicChanged.RemoveListener(UpdateCurrentMusicName);
            Plugin.currentSectionEntry.SettingChanged -= CurrentSectionEntry_SettingChanged;
        }

        internal void UpdateCurrentMusicName(string trackName)
        {
            musicLabel.SetTextSafe(trackName, true);
            headerTitle.text = $"Currently Playing: <color=yellow>{trackName}</color>";
        }

        internal void SetSection(string sectionName, bool updateEntry)
        {
            if (!scrollRectsSection.ContainsKey(sectionName))
            {
                Plugin.LogSource.LogError($"Section named {sectionName} doesn't exist");
                return;
            }
            if (updateEntry)
            {
                Plugin.SetBrowsableSection(sectionName, Plugin.currentSectionEntry.Value != sectionName);
                Plugin.currentSectionEntry.Value = sectionName;
            }
            foreach (KeyValuePair<string, SettingsTab> pair in tabsSection)
            {
                pair.Value.SetActivated(pair.Key == sectionName);
            }
        }

        void InitializeTabs()
        {
            /*
             * This is not a typo
             / Content holds tab and the actual content
             / We are only destroying the content, not including the tab
             */
            Destroy(transform.Find("Content/Content").gameObject);
            Destroy(headerTitle.GetComponent<Localize>());
            var tabs = transform.Find("Content/Tabs");

            // The last is a button hint so we want to exclude it from being destroyed
            for (var i = 2; i < tabs.childCount - 1; i++)
            {
                Destroy(tabs.GetChild(i).gameObject);
            }

            tabsSection[Plugin.MUSIC_SECTION] = tabs.GetChild(0).GetComponent<SettingsTab>();
            tabsSection[Plugin.SFX_SECTION] = tabs.GetChild(1).GetComponent<SettingsTab>();
        }

        void CreateSection(string sectionName, SettingsTab settingsTab, List<ConfigEntryBase> entries)
        {
            var section = new GameObject($"{sectionName} Section").AddComponent<RectTransform>();
            section.gameObject.layer = LayerMask.NameToLayer("UI");
            section.transform.SetParent(ContentLayout.transform, false);
            section.anchoredPosition = new Vector2(0, 30);
            scrollRectsSection[sectionName] = CreateScrollRect(section);
            Traverse.Create(settingsTab).Field("sectionRoot").SetValue(section.gameObject);

            var sectionTMP = settingsTab.GetComponentInChildren<TextMeshProUGUI>(true);
            DestroyImmediate(sectionTMP.GetComponent<Localize>());
            sectionTMP.text = sectionName;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                soundList.Add(CreateItem(entry, scrollRectsSection[sectionName], i % 2 == 1));
            }
            CreateEmptyItem(scrollRectsSection[sectionName]);
        }

        ScrollRect CreateScrollRect(Transform parent)
        {
            var scrollRect = new GameObject("ScrollRect").AddComponent<ScrollRect>();
            scrollRect.transform.localPosition = new Vector3(0, -20);
            scrollRect.transform.SetParent(parent, false);
            scrollRect.gameObject.layer = LayerMask.NameToLayer("UI");

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollRect.transform, false);
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.viewport.anchoredPosition = new Vector2(0, 340);
            scrollRect.viewport.pivot = new Vector2(0.5f, 1f);
            scrollRect.viewport.sizeDelta = new Vector2(1000, 720);
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            var bgBanner = transform.Find("Bg container/Image bg banner");
            viewport.GetComponent<Image>().sprite = bgBanner.GetComponent<Image>().sprite;
            viewport.GetComponent<Image>().type = Image.Type.Sliced;

            var targetAspectRatio = bgBanner.GetComponent<AspectRatioFitter>();
            var aspectRatioFitter = viewport.AddComponent<AspectRatioFitter>();
            aspectRatioFitter.aspectRatio = targetAspectRatio.aspectRatio;
            aspectRatioFitter.aspectMode = targetAspectRatio.aspectMode;

            var contentObj = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(viewport.transform, false);
            var content = contentObj.GetComponent<RectTransform>();
            content.pivot = new Vector2(0.5f, 1f);
            scrollRect.content = content;
            scrollRect.content.sizeDelta = scrollRect.viewport.sizeDelta;

            scrollRect.vertical = true;
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 80;
            scrollRect.inertia = false;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.verticalScrollbar = CreateVerticalScrollbar(scrollRect.transform);

            contentObj.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var gridLayout = content.GetComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(1000, 100);
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 1;

            return scrollRect;
        }

        Scrollbar CreateVerticalScrollbar(Transform parent)
        {
            var scrollbarGO = new GameObject("Vertical Scrollbar", typeof(Scrollbar), typeof(Image));
            var scrollbar = scrollbarGO.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scrollbarGO.GetComponent<Image>().type = Image.Type.Sliced;
            scrollbarGO.GetComponent<Image>().sprite = Plugin.scrollbarAtlas.GetSprite("SH_Bg_Scrollbar");

            var rt = scrollbarGO.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchoredPosition = new Vector2(460, 285);
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(24, 510);

            var slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
            var slidingAreaRT = slidingArea.GetComponent<RectTransform>();
            slidingAreaRT.SetParent(scrollbarGO.transform, false);
            slidingAreaRT.anchorMin = Vector2.zero;
            slidingAreaRT.anchorMax = Vector2.one;
            slidingAreaRT.sizeDelta = Vector2.zero;

            var handle = new GameObject("Handle", typeof(Image));
            var handleRT = handle.GetComponent<RectTransform>();
            handleRT.SetParent(slidingArea.transform, false);
            handleRT.anchorMin = new Vector2(0, 0.6831f);
            handleRT.anchorMax = new Vector2(1, 0);
            handleRT.offsetMax = Vector2.zero;
            handleRT.offsetMin = Vector2.zero;
            handleRT.pivot = new Vector2(0.5f, 0.5f);
            handleRT.sizeDelta = Vector2.zero;
            handleRT.GetComponent<Image>().type = Image.Type.Sliced;
            handleRT.GetComponent<Image>().sprite = Plugin.scrollbarAtlas.GetSprite("CMP_Scrollbar");

            var highlight = new GameObject("Target Graphic", typeof(Image));
            highlight.transform.SetParent(handleRT.transform, false);
            var highlightImage = highlight.GetComponent<Image>();
            highlightImage.rectTransform.sizeDelta = Vector2.one * 20;
            highlightImage.rectTransform.anchorMax = Vector2.one;
            highlightImage.rectTransform.anchorMin = Vector2.zero;
            highlightImage.rectTransform.offsetMax = Vector2.one * 10;
            highlightImage.rectTransform.offsetMin = Vector2.one * -10;
            highlightImage.type = Image.Type.Sliced;
            highlightImage.sprite = Sprite.Create(new Texture2D(0, 0), Rect.zero, Vector2.zero);
            var highlightSpr = Plugin.scrollbarAtlas.GetSprite("SH_Square_Border_Highlight");

            scrollbar.handleRect = handleRT;
            scrollbar.targetGraphic = highlightImage;
            scrollbar.transition = Selectable.Transition.SpriteSwap;
            scrollbar.spriteState = new SpriteState
            {
                pressedSprite = highlightSpr,
                highlightedSprite = highlightSpr,
                selectedSprite = highlightSpr,
            };

            return scrollbar;
        }

        GameObject CreateEmptyItem(ScrollRect scrollRect)
        {
            var emptyGO = new GameObject("Empty", typeof(RectTransform)).AddComponent<ContentSizeFitter>();
            emptyGO.transform.SetParent(scrollRect.content, false);
            emptyGO.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            emptyGO.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return emptyGO.gameObject;
        }

        SoundInfo CreateItem(ConfigEntryBase entry, ScrollRect scrollRect, bool createBG)
        {
            var soundInfo = new GameObject(entry.Definition.Key).AddComponent<SoundInfo>();
            soundInfo.transform.SetParent(scrollRect.content, false);
            if (createBG)
            {
                var bg = new GameObject("BG").AddComponent<Image>();
                bg.color = new Color(0, 0, 0, 0.5f);
                bg.rectTransform.anchorMin = Vector2.zero;
                bg.rectTransform.anchorMax = Vector2.one;
                bg.transform.SetParent(soundInfo.transform, false);
            }
            soundInfo.Set(SettingsScreen, entry);
            return soundInfo;
        }

        /// <summary>
        /// Get the screen input applied by the game, then apply to the list of objects we have created
        /// <br/><br/>
        /// NOTE: This is very sensitive, if there's error sometimes it doesn't output anything at all despite something is clearly not working as intended
        /// </summary>
        /// <param name="mapping"></param>
        /// <param name="triggeredUI"></param>
        /// <param name="triggeredMappingID"></param>
        /// <returns></returns>
        public override bool ApplyScreenInput(CoreInputControlMapping mapping, IGameUIComponent triggeredUI, InputManager.Controls triggeredMappingID)
        {
            if (base.ApplyScreenInput(mapping, triggeredUI, triggeredMappingID))
            {
                return true;
            }
            if (soundChangerButton != null && soundChangerButton.TryTrigger(mapping, triggeredUI, triggeredMappingID))
            {
                Open();
                return true;
            }
            foreach (KeyValuePair<string, SettingsTab> pair in tabsSection)
            {
                if (pair.Value != null && pair.Value.TryTrigger(mapping, triggeredUI, triggeredMappingID))
                {
                    SetSection(pair.Key, true);
                    return true;
                }
            }
            foreach (SoundInfo soundInfo in soundList)
            {
                if (!soundInfo.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (soundInfo.TryTrigger(mapping, triggeredUI, triggeredMappingID))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Close the dialog
        /// </summary>
        public override void Close()
        {
            base.Close();
            SoundManager.PlaySfxSignal.Dispatch("UI_Cancel");
        }


        internal static void CreateDialog(SettingsScreen settingScreen)
        {
            if (settingScreen == null)
            {
                return;
            }
            var settingTraverse = Traverse.Create(settingScreen);
            var settingDialog = settingTraverse.Field<SettingsDialog>("settingsDialog").Value;
            var pauseDialog = settingTraverse.Field<PauseDialog>("pauseDialog").Value;
            var newSetting = Instantiate(settingDialog, settingDialog.transform.parent);
            var newSettingGO = newSetting.gameObject;
            DestroyImmediate(newSetting);
            SoundDialog.Instance = newSettingGO.AddComponent<SoundDialog>();
            SoundDialog.Instance.CreateSoundChangerButton(pauseDialog);
        }
    }
}
