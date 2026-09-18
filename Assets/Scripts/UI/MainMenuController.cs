using System;
using System.Collections.Generic;
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
        const string CopyrightLine = "© 2026 Escape Productions. All rights reserved.";

        VisualElement _panelRoot;
        VisualElement _panelDifficulty;
        VisualElement _panelContact;
        VisualElement _panelAbout;
        VisualElement _panelDialog;
        VisualElement _panelLocalSaves;
        VisualElement _localSavesRows;
        VisualElement _screen;
        Label _brandTitle;
        Label _subtitle;
        Label _aboutVersion;
        Label _dialogMessage;
        Label _localSavesEmpty;
        LocalSaveMenuPresenter _presenter;
        bool _callbacksBound;

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
            _panelDialog = root.Q<VisualElement>("panel-dialog");
            _panelLocalSaves = root.Q<VisualElement>("panel-local-saves");
            _localSavesRows = root.Q<VisualElement>("local-saves-rows");
            _aboutVersion = root.Q<Label>("about-version");
            _dialogMessage = root.Q<Label>("dialog-message");
            _localSavesEmpty = root.Q<Label>("local-saves-empty");
            DisableRichText(_dialogMessage);
            DisableRichText(_localSavesEmpty);
            var aboutCopyright = root.Q<Label>("about-copyright");
            if (aboutCopyright != null)
                aboutCopyright.text = CopyrightLine;

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
                root.Q<Button>("btn-contact")?.RegisterCallback<ClickEvent>(_ => ShowOnly(_panelContact));
                root.Q<Button>("btn-about")?.RegisterCallback<ClickEvent>(_ => ShowAbout());

                root.Q<Button>("btn-diff-sandbox")?.RegisterCallback<ClickEvent>(_ => StartTower(GameDifficulty.Sandbox));
                root.Q<Button>("btn-diff-easy")?.RegisterCallback<ClickEvent>(_ => StartTower(GameDifficulty.Easy));
                root.Q<Button>("btn-diff-normal")?.RegisterCallback<ClickEvent>(_ => StartTower(GameDifficulty.Normal));
                root.Q<Button>("btn-diff-hard")?.RegisterCallback<ClickEvent>(_ => StartTower(GameDifficulty.Hard));
                root.Q<Button>("btn-diff-extreme")?.RegisterCallback<ClickEvent>(_ => StartTower(GameDifficulty.Extreme));
                root.Q<Button>("btn-diff-back")?.RegisterCallback<ClickEvent>(_ => ShowOnly(_panelRoot));

                root.Q<Button>("btn-email")?.RegisterCallback<ClickEvent>(_ => Application.OpenURL("mailto:" + ContactEmail));
                root.Q<Button>("btn-website")?.RegisterCallback<ClickEvent>(_ => Application.OpenURL(ContactWebsite));
                root.Q<Button>("btn-contact-back")?.RegisterCallback<ClickEvent>(_ => ShowOnly(_panelRoot));
                root.Q<Button>("btn-about-back")?.RegisterCallback<ClickEvent>(_ => ShowOnly(_panelRoot));
                root.Q<Button>("btn-local-saves-back")?.RegisterCallback<ClickEvent>(_ => HideLocalSavesPanel());
                root.Q<Button>("btn-dialog-ok")?.RegisterCallback<ClickEvent>(_ => HideDialog());
                _callbacksBound = true;
            }

            ShowOnly(_panelRoot);
        }

        public void ShowLocalSavesPanel()
        {
            RefreshLocalSaves();
            ShowOnly(_panelLocalSaves);
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

        void ShowDialog(string message)
        {
            if (_dialogMessage != null)
                _dialogMessage.text = message;
            if (_panelDialog != null)
                _panelDialog.RemoveFromClassList("hidden");
        }

        void HideDialog()
        {
            if (_panelDialog != null)
                _panelDialog.AddToClassList("hidden");
        }

        void ShowOnly(VisualElement panel)
        {
            HideDialog();
            SetVisible(_panelRoot, panel == _panelRoot);
            SetVisible(_panelDifficulty, panel == _panelDifficulty);
            SetVisible(_panelContact, panel == _panelContact);
            SetVisible(_panelAbout, panel == _panelAbout);
            SetVisible(_panelLocalSaves, panel == _panelLocalSaves);

            var compact = panel == _panelDifficulty || panel == _panelLocalSaves;
            _screen?.EnableInClassList("compact-header", compact);
            _brandTitle?.EnableInClassList("brand-compact", compact);
            _subtitle?.EnableInClassList("hidden", compact);
        }

        static void SetVisible(VisualElement el, bool visible)
        {
            if (el == null) return;
            if (visible) el.RemoveFromClassList("hidden");
            else el.AddToClassList("hidden");
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
    }
}
