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
        VisualElement _screen;
        Label _brandTitle;
        Label _subtitle;
        Label _aboutVersion;
        Label _dialogMessage;

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null || doc.rootVisualElement == null) return;

            var root = doc.rootVisualElement;
            _screen = root.Q<VisualElement>("screen");
            _brandTitle = root.Q<Label>("brand-title");
            _subtitle = root.Q<Label>("subtitle");
            _panelRoot = root.Q<VisualElement>("panel-root");
            _panelDifficulty = root.Q<VisualElement>("panel-difficulty");
            _panelContact = root.Q<VisualElement>("panel-contact");
            _panelAbout = root.Q<VisualElement>("panel-about");
            _panelDialog = root.Q<VisualElement>("panel-dialog");
            _aboutVersion = root.Q<Label>("about-version");
            _dialogMessage = root.Q<Label>("dialog-message");
            var aboutCopyright = root.Q<Label>("about-copyright");
            if (aboutCopyright != null)
                aboutCopyright.text = CopyrightLine;

            root.Q<Button>("btn-new-game")?.RegisterCallback<ClickEvent>(_ => ShowOnly(_panelDifficulty));
            root.Q<Button>("btn-save-game")?.RegisterCallback<ClickEvent>(_ => ShowDialog("Not available yet."));
            root.Q<Button>("btn-load-game")?.RegisterCallback<ClickEvent>(_ => ShowDialog("Not available yet."));
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
            root.Q<Button>("btn-dialog-ok")?.RegisterCallback<ClickEvent>(_ => HideDialog());

            ShowOnly(_panelRoot);
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

            var compact = panel == _panelDifficulty;
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

        public void StartTower(GameDifficulty difficulty)
        {
            GameSession.StartNewGame(difficulty);
            SceneManager.LoadScene(TowerSceneName);
        }
    }
}
