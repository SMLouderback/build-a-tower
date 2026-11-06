using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BuildATower.Tests
{
    public sealed class PlaytestMenuFlowTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var menu in UnityEngine.Object.FindObjectsByType<MainMenuController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(menu.gameObject);

            foreach (var hud in UnityEngine.Object.FindObjectsByType<TowerHudController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(hud.gameObject);
        }

        [Test]
        public void Feedback_button_opens_panel()
        {
            var menu = CreateBoundMenu(out var root);

            Assert.IsNotNull(root.Q<Button>("btn-feedback"));
            var panel = root.Q("panel-feedback");
            Assert.IsNotNull(panel);
            Assert.IsTrue(panel.ClassListContains("hidden"));

            menu.ShowFeedbackPanel();

            Assert.IsFalse(panel.ClassListContains("hidden"));
            Assert.IsTrue(root.Q("panel-root").ClassListContains("hidden"));
        }

        [Test]
        public void Submenu_back_buttons_return_to_root()
        {
            var menu = CreateBoundMenu(out var root);
            var panelRoot = root.Q("panel-root");
            Assert.IsNotNull(panelRoot);

            var cases = new (string openButton, string panel, string backButton)[]
            {
                ("btn-new-game", "panel-difficulty", "btn-diff-back"),
                ("btn-load-game", "panel-local-saves", "btn-local-saves-back"),
                ("btn-account", "panel-account", "btn-account-back"),
                ("btn-feedback", "panel-feedback", "btn-feedback-back"),
                ("btn-contact", "panel-contact", "btn-contact-back"),
                ("btn-about", "panel-about", "btn-about-back"),
            };

            foreach (var (openButton, panelName, backButton) in cases)
            {
                var open = root.Q<Button>(openButton);
                var panel = root.Q(panelName);
                var back = root.Q<Button>(backButton);
                Assert.IsNotNull(open, openButton);
                Assert.IsNotNull(panel, panelName);
                Assert.IsNotNull(back, backButton);
                Assert.AreEqual("Back to Main Menu", back.text, backButton);

                using (var openEvt = ClickEvent.GetPooled())
                {
                    openEvt.target = open;
                    open.SendEvent(openEvt);
                }

                Assert.IsFalse(panel.ClassListContains("hidden"), panelName + " should open");
                Assert.IsTrue(panelRoot.ClassListContains("hidden"), "root hidden while " + panelName);

                using (var backEvt = ClickEvent.GetPooled())
                {
                    backEvt.target = back;
                    back.SendEvent(backEvt);
                }

                Assert.IsTrue(panel.ClassListContains("hidden"), panelName + " should close");
                Assert.IsFalse(panelRoot.ClassListContains("hidden"), "root visible after " + backButton);
            }
        }

        [Test]
        public async Task Empty_send_shows_write_a_note_first()
        {
            var posted = false;
            var menu = CreateBoundMenu(out var root);
            menu.ConfigureFeedback(Client(_ =>
            {
                posted = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }));
            menu.ShowFeedbackPanel();

            root.Q<TextField>("feedback-note").value = "  ";
            await menu.TrySendFeedback();

            Assert.IsFalse(posted);
            Assert.AreEqual("Write a note first.", root.Q<Label>("feedback-status").text);
        }

        [Test]
        public async Task Successful_send_clears_note_but_keeps_contact_fields()
        {
            var menu = CreateBoundMenu(out var root);
            menu.ConfigureFeedback(Client(_ => new HttpResponseMessage(HttpStatusCode.NoContent)));
            menu.ShowFeedbackPanel();

            root.Q<TextField>("feedback-note").value = "The lobby flow felt good.";
            root.Q<TextField>("feedback-name").value = "Pat";
            root.Q<TextField>("feedback-email").value = "pat@example.com";
            await menu.TrySendFeedback();

            Assert.AreEqual("Thanks — sent.", root.Q<Label>("feedback-status").text);
            Assert.AreEqual(string.Empty, root.Q<TextField>("feedback-note").value);
            Assert.AreEqual("Pat", root.Q<TextField>("feedback-name").value);
            Assert.AreEqual("pat@example.com", root.Q<TextField>("feedback-email").value);
        }

        [Test]
        public async Task Invalid_email_shows_validation_status_without_posting()
        {
            var posted = false;
            var menu = CreateBoundMenu(out var root);
            menu.ConfigureFeedback(Client(_ =>
            {
                posted = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }));
            menu.ShowFeedbackPanel();

            root.Q<TextField>("feedback-note").value = "Hello";
            root.Q<TextField>("feedback-email").value = "not-an-email";
            await menu.TrySendFeedback();

            Assert.IsFalse(posted);
            Assert.AreEqual("Enter a valid email or leave it blank.", root.Q<Label>("feedback-status").text);
        }

        [Test]
        public void Pause_feedback_test_hook_opens_feedback_state()
        {
            var hud = CreateHud();

            hud.OpenPauseFeedbackForTests();

            Assert.AreEqual("Feedback", GetPauseUi(hud));
        }

        [Test]
        public async Task Pause_feedback_empty_send_shows_write_a_note_first_without_posting()
        {
            var posted = false;
            var hud = CreateHud();
            hud.ConfigureFeedback(Client(_ =>
            {
                posted = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }));
            hud.OpenPauseFeedbackForTests();
            hud.SetPauseFeedbackDraftForTests("  ");

            await hud.TrySendPauseFeedback();

            Assert.IsFalse(posted);
            Assert.AreEqual("Write a note first.", hud.PauseFeedbackStatusText);
        }

        [Test]
        public void Pause_feedback_escape_returns_to_pause_root()
        {
            var hud = CreateHud();
            hud.OpenPauseFeedbackForTests();

            InvokeHud(hud, "HandlePauseEscape");

            Assert.AreEqual("Paused", GetPauseUi(hud));
        }

        static MainMenuController CreateBoundMenu(out VisualElement root)
        {
            var gameObject = new GameObject("Playtest Menu");
            gameObject.SetActive(false);
            var menu = gameObject.AddComponent<MainMenuController>();
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/UI/MainMenu.uxml");
            Assert.IsNotNull(asset, "MainMenu.uxml must be importable.");
            root = asset.CloneTree();
            menu.Bind(root);
            return menu;
        }

        static TowerHudController CreateHud()
        {
            var gameObject = new GameObject("Playtest Pause HUD");
            gameObject.SetActive(false);
            return gameObject.AddComponent<TowerHudController>();
        }

        static string GetPauseUi(TowerHudController hud)
        {
            var field = typeof(TowerHudController).GetField("_pauseUi", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return field.GetValue(hud).ToString();
        }

        static void InvokeHud(TowerHudController hud, string methodName)
        {
            var method = typeof(TowerHudController).GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing method " + methodName);
            method.Invoke(hud, null);
        }

        static PlaytestFeedbackClient Client(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            return new PlaytestFeedbackClient(
                new PlaytestConfig(),
                new HttpClient(new StubHandler(responder))
                {
                    BaseAddress = new Uri("https://escapeproductions.biz/")
                });
        }

        sealed class StubHandler : HttpMessageHandler
        {
            readonly Func<HttpRequestMessage, HttpResponseMessage> responder;

            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
            {
                this.responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(responder(request));
            }
        }
    }
}
