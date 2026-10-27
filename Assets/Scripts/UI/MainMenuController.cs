using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BuildATower
{
    public sealed class MainMenuController : MonoBehaviour
    {
        const string TowerSceneName = "TowerSandbox";
        const string ContactEmail = "escapemobileproductions@gmail.com";
        const string ContactWebsite = "https://escapeproductions.biz/";
        const string FacebookUrl = "https://www.facebook.com/EScapeMProd";
        const string CopyrightLine = "© 2026 Escape Productions. All rights reserved.";

        VisualElement _panelRoot;
        VisualElement _panelDifficulty;
        VisualElement _panelContact;
        VisualElement _panelAbout;
        VisualElement _panelFeedback;
        VisualElement _panelDialog;
        VisualElement _panelLocalSaves;
        VisualElement _panelAccount;
        VisualElement _panelExit;
        VisualElement _localSavesRows;
        VisualElement _screen;
        Label _brandTitle;
        Label _subtitle;
        Label _aboutVersion;
        Label _dialogMessage;
        Label _localSavesEmpty;
        Label _accountStatus;
        TextField _feedbackNote;
        TextField _feedbackName;
        TextField _feedbackEmail;
        Label _feedbackStatus;
        LocalSaveMenuPresenter _presenter;
        ICloudSlotSource _cloudSlots;
        PlaytestFeedbackClient _feedbackClient;
        bool _callbacksBound;

        public Action ExitGame { get; set; }

        public void ConfigureFeedback(PlaytestFeedbackClient feedbackClient)
        {
            _feedbackClient = feedbackClient;
        }

        public void ConfigureLocalSaves(
            LocalSaveRepository repository,
            SaveCoordinator coordinator,
            Action loadTowerScene,
            TimeZoneInfo displayTimeZone = null)
        {
            _presenter = new LocalSaveMenuPresenter(
                repository,
                coordinator,
                loadTowerScene,
                displayTimeZone ?? TimeZoneInfo.Local);
            if (_cloudSlots != null)
                _presenter.ConfigureCloudSlots(_cloudSlots);
        }

        public void ConfigureCloudSlots(ICloudSlotSource cloudSlots)
        {
            _cloudSlots = cloudSlots;
            Presenter.ConfigureCloudSlots(cloudSlots);
        }

        public IReadOnlyList<LocalSaveSummary> RefreshLocalSaves()
        {
            var summaries = Presenter.RefreshLocalSaves();
            RebuildSaveRows(summaries);
            return summaries;
        }

        public bool TryPrepareLoad(string saveId, out string errorMessage)
        {
            return Presenter.TryPrepareLoad(saveId, out errorMessage);
        }

        public bool TryPrepareRecovery(string saveId, int recoveryIndex, out string errorMessage)
        {
            return Presenter.TryPrepareRecovery(saveId, recoveryIndex, out errorMessage);
        }

        public IReadOnlyList<LocalRecoveryChoice> ListRecoveries(string saveId)
        {
            return Presenter.ListRecoveries(saveId);
        }

        public LocalSaveRowPresentation PresentRow(LocalSaveSummary summary)
        {
            return Presenter.PresentRow(summary);
        }

        public void Bind(VisualElement root)
        {
            if (root == null)
                return;

            _screen = root.Q<VisualElement>("screen");
            _brandTitle = root.Q<Label>("brand-title");
            _subtitle = root.Q<Label>("subtitle");
            _panelRoot = root.Q<VisualElement>("panel-root");
            _panelDifficulty = root.Q<VisualElement>("panel-difficulty");
            _panelContact = root.Q<VisualElement>("panel-contact");
            _panelAbout = root.Q<VisualElement>("panel-about");
            _panelFeedback = root.Q<VisualElement>("panel-feedback");
            _panelDialog = root.Q<VisualElement>("panel-dialog");
            _panelLocalSaves = root.Q<VisualElement>("panel-local-saves");
            _panelAccount = root.Q<VisualElement>("panel-account");
            _panelExit = root.Q<VisualElement>("panel-exit");
            _localSavesRows = root.Q<VisualElement>("local-saves-rows");
            _aboutVersion = root.Q<Label>("about-version");
            _dialogMessage = root.Q<Label>("dialog-message");
            _localSavesEmpty = root.Q<Label>("local-saves-empty");
            _accountStatus = root.Q<Label>("account-status");
            _feedbackNote = root.Q<TextField>("feedback-note");
            _feedbackName = root.Q<TextField>("feedback-name");
            _feedbackEmail = root.Q<TextField>("feedback-email");
            _feedbackStatus = root.Q<Label>("feedback-status");
            DisableRichText(_dialogMessage);
            DisableRichText(_localSavesEmpty);
            DisableRichText(_accountStatus);
            DisableRichText(_feedbackStatus);
            var aboutCopyright = root.Q<Label>("about-copyright");
            if (aboutCopyright != null)
                aboutCopyright.text = CopyrightLine;

            ApplyLobbyAtmosphere(root);

            if (_screen != null)
            {
                _screen.focusable = true;
                _screen.tabIndex = 0;
            }

            var saveButton = root.Q<Button>("btn-save-game");
            if (saveButton != null)
            {
                saveButton.SetEnabled(false);
                saveButton.tooltip = LocalSaveMenuPresenter.SaveDisabledTooltip;
            }

            if (!_callbacksBound)
            {
                root.Q<Button>("btn-new-game")?.RegisterCallback<ClickEvent>(_ => ShowOnly(_panelDifficulty));
                root.Q<Button>("btn-load-game")?.RegisterCallback<ClickEvent>(_ => ShowLocalSavesPanel());
                root.Q<Button>("btn-account")?.RegisterCallback<ClickEvent>(_ => ShowAccountPanel());
                root.Q<Button>("btn-feedback")?.RegisterCallback<ClickEvent>(_ => ShowFeedbackPanel());
                root.Q<Button>("btn-contact")?.RegisterCallback<ClickEvent>(_ => ShowOnly(_panelContact));
                root.Q<Button>("btn-about")?.RegisterCallback<ClickEvent>(_ => ShowAbout());
                root.Q<Button>("btn-reset-layout")?.RegisterCallback<ClickEvent>(_ => ResetBuildHudLayout());
                root.Q<Button>("btn-exit")?.RegisterCallback<ClickEvent>(_ => RequestExit());

                root.Q<Button>("btn-diff-sandbox")?.RegisterCallback<ClickEvent>(_ => StartTower(GameDifficulty.Sandbox));
                root.Q<Button>("btn-diff-easy")?.RegisterCallback<ClickEvent>(_ => StartTower(GameDifficulty.Easy));
                root.Q<Button>("btn-diff-normal")?.RegisterCallback<ClickEvent>(_ => StartTower(GameDifficulty.Normal));
                root.Q<Button>("btn-diff-hard")?.RegisterCallback<ClickEvent>(_ => StartTower(GameDifficulty.Hard));
                root.Q<Button>("btn-diff-extreme")?.RegisterCallback<ClickEvent>(_ => StartTower(GameDifficulty.Extreme));
                root.Q<Button>("btn-diff-back")?.RegisterCallback<ClickEvent>(_ => ShowOnly(_panelRoot));

                root.Q<Button>("btn-email")?.RegisterCallback<ClickEvent>(_ => Application.OpenURL("mailto:" + ContactEmail));
                root.Q<Button>("btn-website")?.RegisterCallback<ClickEvent>(_ => Application.OpenURL(ContactWebsite));
                root.Q<Button>("btn-contact-back")?.RegisterCallback<ClickEvent>(_ => ShowOnly(_panelRoot));
                root.Q<Button>("btn-feedback-send")?.RegisterCallback<ClickEvent>(evt =>
                {
                    _ = TrySendFeedback();
                });
                root.Q<Button>("btn-feedback-facebook")?.RegisterCallback<ClickEvent>(_ => Application.OpenURL(FacebookUrl));
                root.Q<Button>("btn-feedback-back")?.RegisterCallback<ClickEvent>(_ => ShowOnly(_panelRoot));
                root.Q<Button>("btn-about-back")?.RegisterCallback<ClickEvent>(_ => ShowOnly(_panelRoot));
                root.Q<Button>("btn-local-saves-back")?.RegisterCallback<ClickEvent>(_ => HideLocalSavesPanel());
                root.Q<Button>("btn-account-back")?.RegisterCallback<ClickEvent>(_ => ShowOnly(_panelRoot));
                root.Q<Button>("btn-account-login")?.RegisterCallback<ClickEvent>(_ => SetAccountPanelState(null, false, "Account login will contact the cloud API."));
                root.Q<Button>("btn-account-register")?.RegisterCallback<ClickEvent>(_ => SetAccountPanelState(null, false, "Registration requires a cloud invite code."));
                root.Q<Button>("btn-account-verify")?.RegisterCallback<ClickEvent>(_ => SetAccountPanelState(null, false, "Enter the verification code from email."));
                root.Q<Button>("btn-account-forgot")?.RegisterCallback<ClickEvent>(_ => SetAccountPanelState(null, false, "Password reset email request queued."));
                root.Q<Button>("btn-dialog-ok")?.RegisterCallback<ClickEvent>(_ => HideDialog());
                root.Q<Button>("btn-exit-confirm")?.RegisterCallback<ClickEvent>(_ => ConfirmExit());
                root.Q<Button>("btn-exit-cancel")?.RegisterCallback<ClickEvent>(_ => CancelExit());
                root.RegisterCallback<KeyDownEvent>(OnRootKeyDown);
                _callbacksBound = true;
            }

            ShowOnly(_panelRoot);
        }

        void OnRootKeyDown(KeyDownEvent evt)
        {
            if (evt == null || evt.keyCode != KeyCode.Escape)
                return;

            if (_panelExit != null && !_panelExit.ClassListContains("hidden"))
            {
                CancelExit();
                evt.StopPropagation();
                return;
            }

            if (_panelDialog != null && !_panelDialog.ClassListContains("hidden"))
            {
                HideDialog();
                evt.StopPropagation();
                return;
            }

            if (_panelRoot != null && _panelRoot.ClassListContains("hidden"))
            {
                ShowOnly(_panelRoot);
                evt.StopPropagation();
            }
        }

        public void ShowLocalSavesPanel()
        {
            RefreshLocalSaves();
            _ = RefreshCloudSlots();
            ShowOnly(_panelLocalSaves);
        }

        public void ShowAccountPanel()
        {
            if (_accountStatus != null && string.IsNullOrEmpty(_accountStatus.text))
                SetAccountPanelState(null, true, string.Empty);
            ShowOnly(_panelAccount);
        }

        public void ShowFeedbackPanel()
        {
            if (_feedbackStatus != null)
                _feedbackStatus.text = string.Empty;
            ShowOnly(_panelFeedback);
        }

        public async Task TrySendFeedback(CancellationToken cancellationToken = default)
        {
            var result = await FeedbackClient.SendAsync(
                new PlaytestFeedbackDraft
                {
                    Message = _feedbackNote?.value,
                    Name = _feedbackName?.value,
                    Email = _feedbackEmail?.value,
                    Version = Application.version
                },
                cancellationToken);

            SetFeedbackStatus(result.Kind);
            if (result.Kind == PlaytestFeedbackSendKind.Sent && _feedbackNote != null)
                _feedbackNote.value = string.Empty;
        }

        public void SetAccountPanelState(string email, bool emailConfirmed, string message)
        {
            if (_accountStatus == null)
                return;

            if (!string.IsNullOrWhiteSpace(message))
            {
                _accountStatus.text = message;
                return;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                _accountStatus.text = string.IsNullOrWhiteSpace(GameSession.CurrentAccountId)
                    ? "Sign in to use three cloud save slots. Local saves still work offline."
                    : "Signed in. Cloud slots are available after email verification.";
                return;
            }

            _accountStatus.text = emailConfirmed
                ? "Signed in as " + email + ". Cloud slots are ready."
                : "Signed in as " + email + ". Verify your email before cloud sync can upload.";
        }

        public async Task<IReadOnlyList<CloudSlotSummary>> RefreshCloudSlots(CancellationToken cancellationToken = default)
        {
            var slots = await Presenter.RefreshCloudSlots(cancellationToken);
            RebuildCloudRows(slots);
            return slots;
        }

        public void HideLocalSavesPanel()
        {
            ClearSaveRows();
            ShowOnly(_panelRoot);
        }

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null || doc.rootVisualElement == null) return;
            Bind(doc.rootVisualElement);
        }

        void ShowAbout()
        {
            if (_aboutVersion != null)
                _aboutVersion.text = $"Version {Application.version}";
            ShowOnly(_panelAbout);
        }

        void ResetBuildHudLayout()
        {
            HudFloatingPanel.ClearRect("bat.buildDock");
            HudFloatingPanel.ClearRect("bat.buildInfo");
            ShowDialog("Build menu layout reset. Dock and info panel will return to defaults next time you enter a tower.");
        }

        void ShowDialog(string message)
        {
            if (_dialogMessage != null)
                _dialogMessage.text = message;
            if (_panelDialog != null)
                _panelDialog.RemoveFromClassList("hidden");
        }

        void HideDialog()
        {
            _panelDialog?.AddToClassList("hidden");
        }

        public void RequestExit()
        {
            HideDialog();
            _panelExit?.RemoveFromClassList("hidden");
        }

        public void CancelExit()
        {
            _panelExit?.AddToClassList("hidden");
        }

        public void ConfirmExit()
        {
            CancelExit();
            (ExitGame ?? QuitProcess)();
        }

        static void QuitProcess()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
        }

        void ShowOnly(VisualElement panel)
        {
            HideDialog();
            CancelExit();
            SetVisible(_panelRoot, panel == _panelRoot);
            SetVisible(_panelDifficulty, panel == _panelDifficulty);
            SetVisible(_panelContact, panel == _panelContact);
            SetVisible(_panelAbout, panel == _panelAbout);
            SetVisible(_panelFeedback, panel == _panelFeedback);
            SetVisible(_panelLocalSaves, panel == _panelLocalSaves);
            SetVisible(_panelAccount, panel == _panelAccount);

            var compact = panel == _panelDifficulty
                          || panel == _panelLocalSaves
                          || panel == _panelAccount
                          || panel == _panelFeedback
                          || panel == _panelContact
                          || panel == _panelAbout;
            _screen?.EnableInClassList("compact-header", compact);
            _brandTitle?.EnableInClassList("brand-compact", compact);
            _subtitle?.EnableInClassList("hidden", compact);
        }

        void ApplyLobbyAtmosphere(VisualElement root)
        {
            var atmosphere = root.Q<VisualElement>("atmosphere");
            if (atmosphere == null)
                return;

            var lobby = LoadMenuTexture("mainmenu_lobby_bg");
            if (lobby != null)
                atmosphere.style.backgroundImage = new StyleBackground(lobby);
        }

        static Texture2D LoadMenuTexture(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            var path = MenuIconArt.ResourcesRoot + id;
            var bytesAsset = Resources.Load<TextAsset>(path);
            var png = bytesAsset != null ? bytesAsset.bytes : null;
            if (png != null && png.Length >= 32)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = id
                };
                if (tex.LoadImage(png, false))
                    return tex;
                UnityEngine.Object.Destroy(tex);
            }

            return Resources.Load<Texture2D>(path);
        }

        static void SetVisible(VisualElement el, bool visible)
        {
            if (el == null) return;
            if (visible)
            {
                el.RemoveFromClassList("hidden");
                el.style.display = DisplayStyle.Flex;
            }
            else
            {
                el.AddToClassList("hidden");
                el.style.display = DisplayStyle.None;
            }
        }

        static Label PlainTextLabel(string text, string name)
        {
            var label = new Label(text ?? string.Empty) { name = name };
            label.enableRichText = false;
            return label;
        }

        static void DisableRichText(Label label)
        {
            if (label != null)
                label.enableRichText = false;
        }

        public void StartTower(GameDifficulty difficulty)
        {
            GameSession.StartNewGame(difficulty);
            SceneManager.LoadScene(TowerSceneName);
        }

        void RebuildSaveRows(IReadOnlyList<LocalSaveSummary> summaries)
        {
            ClearSaveRows();
            if (_localSavesEmpty != null)
            {
                _localSavesEmpty.text = LocalSaveMenuPresenter.EmptySavesMessage;
                SetVisible(_localSavesEmpty, summaries == null || summaries.Count == 0);
            }

            if (_localSavesRows == null || summaries == null)
                return;

            foreach (var summary in summaries)
            {
                var row = PresentRow(summary);
                var rowElement = new VisualElement { name = "save-row-" + summary.SaveId };
                rowElement.AddToClassList("save-row");

                var name = PlainTextLabel(row.TowerName, "save-name-" + summary.SaveId);
                name.AddToClassList("save-name");
                rowElement.Add(name);

                if (row.CanLoadCurrent)
                {
                    var meta = PlainTextLabel(row.MetaText, "save-meta-" + summary.SaveId);
                    meta.AddToClassList("save-meta");
                    rowElement.Add(meta);
                }
                else
                {
                    var status = PlainTextLabel(row.StatusMessage, "save-status-" + summary.SaveId);
                    status.AddToClassList("save-status");
                    rowElement.Add(status);
                }

                var load = new Button { name = "btn-load-" + summary.SaveId, text = "Load" };
                load.AddToClassList("menu-button");
                load.SetEnabled(row.CanLoadCurrent);
                var saveId = summary.SaveId;
                if (row.CanLoadCurrent)
                    load.RegisterCallback<ClickEvent>(_ => TryLoadCurrent(saveId));
                rowElement.Add(load);

                if (!row.CanLoadCurrent)
                {
                    foreach (var recovery in ListRecoveries(summary.SaveId))
                    {
                        var recoveryLabel = PlainTextLabel(
                            recovery.LabelText,
                            "recovery-label-" + summary.SaveId + "-" + recovery.RecoveryIndex);
                        recoveryLabel.AddToClassList("save-meta");
                        rowElement.Add(recoveryLabel);

                        var recoveryButton = new Button
                        {
                            name = "btn-load-recovery-" + summary.SaveId + "-" + recovery.RecoveryIndex,
                            text = "Load recovery " + recovery.RecoveryIndex
                        };
                        recoveryButton.AddToClassList("menu-button");
                        recoveryButton.SetEnabled(recovery.CanLoad);
                        var recoveryIndex = recovery.RecoveryIndex;
                        recoveryButton.RegisterCallback<ClickEvent>(_ => TryLoadRecovery(saveId, recoveryIndex));
                        rowElement.Add(recoveryButton);
                    }
                }

                _localSavesRows.Add(rowElement);
            }
        }

        void RebuildCloudRows(IReadOnlyList<CloudSlotSummary> slots)
        {
            if (_localSavesRows == null || slots == null || slots.Count == 0)
                return;

            var anyVisibleCloud = false;
            foreach (var summary in slots)
            {
                if (summary == null || !summary.Occupied)
                    continue;

                anyVisibleCloud = true;
                var row = Presenter.PresentCloudRow(summary);
                var rowElement = new VisualElement { name = "cloud-save-row-" + summary.SlotId };
                rowElement.AddToClassList("save-row");
                rowElement.AddToClassList("cloud-save-row");

                var name = PlainTextLabel(row.TowerName, "cloud-save-name-" + summary.SlotId);
                name.AddToClassList("save-name");
                rowElement.Add(name);

                var meta = PlainTextLabel(row.MetaText, "cloud-save-meta-" + summary.SlotId);
                meta.AddToClassList("save-meta");
                rowElement.Add(meta);

                var load = new Button { name = "btn-load-cloud-" + summary.SlotId, text = "Load cloud" };
                load.AddToClassList("menu-button");
                load.SetEnabled(row.CanLoad);
                var slotId = summary.SlotId;
                load.RegisterCallback<ClickEvent>(_ => ConfirmCloudLoad(slotId));
                rowElement.Add(load);
                _localSavesRows.Add(rowElement);
            }

            if (anyVisibleCloud && _localSavesEmpty != null)
                SetVisible(_localSavesEmpty, false);
        }

        void TryLoadCurrent(string saveId)
        {
            if (!TryPrepareLoad(saveId, out var error))
                ShowDialog(error);
        }

        void TryLoadRecovery(string saveId, int recoveryIndex)
        {
            if (!TryPrepareRecovery(saveId, recoveryIndex, out var error))
                ShowDialog(error);
        }

        void ConfirmCloudLoad(int slotId)
        {
            ShowDialog("Cloud slot "
                       + slotId
                       + " will replace the current tower after loading. Save locally first if needed.");
        }

        void ClearSaveRows()
        {
            _localSavesRows?.Clear();
        }

        LocalSaveMenuPresenter Presenter
        {
            get
            {
                if (_presenter != null)
                    return _presenter;

                var repository = LocalSaveRepository.CreateDefault();
                _presenter = new LocalSaveMenuPresenter(
                    repository,
                    new SaveCoordinator(repository),
                    () => SceneManager.LoadScene(TowerSceneName),
                    TimeZoneInfo.Local);
                return _presenter;
            }
        }

        PlaytestFeedbackClient FeedbackClient => _feedbackClient ?? (_feedbackClient = new PlaytestFeedbackClient());

        void SetFeedbackStatus(PlaytestFeedbackSendKind kind)
        {
            if (_feedbackStatus == null)
                return;

            switch (kind)
            {
                case PlaytestFeedbackSendKind.Sent:
                    _feedbackStatus.text = "Thanks — sent.";
                    break;
                case PlaytestFeedbackSendKind.RejectedEmpty:
                    _feedbackStatus.text = "Write a note first.";
                    break;
                case PlaytestFeedbackSendKind.RejectedEmail:
                    _feedbackStatus.text = "Enter a valid email or leave it blank.";
                    break;
                case PlaytestFeedbackSendKind.RateLimited:
                    _feedbackStatus.text = "Too many tries. Wait a minute.";
                    break;
                default:
                    _feedbackStatus.text = "Couldn't send.";
                    break;
            }
        }
    }
}
