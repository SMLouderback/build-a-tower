using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BuildATower;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BuildATower.Tests
{
    public sealed class CloudSaveMenuTests
    {
        string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "bat-cloud-menu-" + Guid.NewGuid().ToString("N"));
            GameSession.ResetForTests();
        }

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

            GameSession.ResetForTests();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void Account_panel_opens_and_save_stays_local_play_only()
        {
            var menu = CreateBoundMenu(out var rootVisual);

            menu.ShowAccountPanel();

            Assert.IsFalse(rootVisual.Q("panel-account").ClassListContains("hidden"));
            Assert.IsTrue(rootVisual.Q("panel-root").ClassListContains("hidden"));
            Assert.IsNotNull(rootVisual.Q<TextField>("account-email"));
            Assert.IsNotNull(rootVisual.Q<TextField>("account-password"));
            Assert.IsNotNull(rootVisual.Q<TextField>("account-invite"));
            Assert.IsNotNull(rootVisual.Q<TextField>("account-verify-token"));
            Assert.IsNotNull(rootVisual.Q<Button>("btn-account-login"));
            Assert.IsNotNull(rootVisual.Q<Button>("btn-account-register"));
            Assert.IsNotNull(rootVisual.Q<Button>("btn-account-verify"));
            Assert.IsNotNull(rootVisual.Q<Button>("btn-account-forgot"));

            var save = rootVisual.Q<Button>("btn-save-game");
            Assert.IsFalse(save.enabledSelf);
            Assert.AreEqual(LocalSaveMenuPresenter.SaveDisabledTooltip, save.tooltip);
        }

        [Test]
        public void Account_panel_shows_verify_prompt_for_unverified_account()
        {
            var menu = CreateBoundMenu(out var rootVisual);

            menu.SetAccountPanelState("player@example.com", false, string.Empty);
            menu.ShowAccountPanel();

            var status = rootVisual.Q<Label>("account-status");
            Assert.IsNotNull(status);
            StringAssert.Contains("player@example.com", status.text);
            StringAssert.Contains("verify", status.text.ToLowerInvariant());
        }

        [Test]
        public async Task Load_panel_lists_cloud_slots_when_signed_in()
        {
            GameSession.SetCurrentAccountId("account-123");
            var slots = new StubCloudSlotSource(
                CloudSlotSummary.OccupiedSlot(
                    1,
                    "Cloud Harbor",
                    12,
                    new DateTime(2026, 9, 26, 12, 30, 0, DateTimeKind.Utc),
                    "Studio PC",
                    731));
            var menu = CreateBoundMenu(out var rootVisual);
            menu.ConfigureCloudSlots(slots);

            menu.ShowLocalSavesPanel();
            await menu.RefreshCloudSlots();

            Assert.IsNotNull(rootVisual.Q("cloud-save-row-1"));
            Assert.AreEqual("Cloud Harbor", rootVisual.Q<Label>("cloud-save-name-1").text);
            StringAssert.Contains("Studio PC", rootVisual.Q<Label>("cloud-save-meta-1").text);
            Assert.IsTrue(rootVisual.Q<Button>("btn-load-cloud-1").enabledSelf);
        }

        [Test]
        public async Task Pause_load_tracks_cloud_slots_for_signed_in_players()
        {
            GameSession.SetCurrentAccountId("account-123");
            var hud = new GameObject("Task9 Hud").AddComponent<TowerHudController>();
            hud.ConfigureCloudSlots(new StubCloudSlotSource(
                CloudSlotSummary.OccupiedSlot(
                    2,
                    "Cloud Heights",
                    3,
                    new DateTime(2026, 9, 26, 14, 0, 0, DateTimeKind.Utc),
                    "Laptop",
                    80)));

            await hud.RefreshPauseCloudSlots();

            Assert.AreEqual(1, hud.PauseCloudSlots.Count);
            Assert.AreEqual(2, hud.PauseCloudSlots[0].SlotId);
            Assert.AreEqual("Cloud Heights", hud.PauseCloudSlots[0].TowerName);
        }

        [Test]
        public async Task Cloud_load_prepares_downloaded_snapshot_through_game_session()
        {
            GameSession.SetCurrentAccountId("account-123");
            var snapshot = Snapshot("cloud-slot-1", "Cloud Harbor", 88000);
            var source = new StubCloudSlotSource(
                CloudSlotSummary.OccupiedSlot(
                    1,
                    "Cloud Harbor",
                    9,
                    new DateTime(2026, 9, 26, 16, 0, 0, DateTimeKind.Utc),
                    "Studio PC",
                    100))
            {
                DownloadResult = CloudSlotDownloadResult.Succeeded(CloudDownload(snapshot, 9))
            };
            var loads = 0;
            TowerSnapshotV1 pendingDuringLoad = null;
            var repository = new LocalSaveRepository(root);
            var presenter = new LocalSaveMenuPresenter(
                repository,
                new SaveCoordinator(repository),
                () =>
                {
                    pendingDuringLoad = GameSession.PendingLoad;
                    loads++;
                },
                TimeZoneInfo.Utc);
            presenter.ConfigureCloudSlots(source);

            var loaded = await presenter.TryPrepareCloudLoad(1, CancellationToken.None, Assert.Fail);

            Assert.IsTrue(loaded);
            Assert.AreEqual(1, loads);
            Assert.IsNotNull(pendingDuringLoad);
            Assert.AreEqual("cloud-slot-1", pendingDuringLoad.saveId);
            Assert.AreEqual("Cloud Harbor", pendingDuringLoad.towerName);
            Assert.AreEqual(88000, pendingDuringLoad.walletBalance);
            Assert.AreEqual(9, pendingDuringLoad.cloudRevision);
        }

        MainMenuController CreateBoundMenu(out VisualElement rootVisual)
        {
            var gameObject = new GameObject("Task9 Menu");
            gameObject.SetActive(false);
            var menu = gameObject.AddComponent<MainMenuController>();
            var repository = new LocalSaveRepository(root);
            menu.ConfigureLocalSaves(
                repository,
                new SaveCoordinator(repository),
                () => { },
                TimeZoneInfo.Utc);

            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Scripts/UI/MainMenu.uxml");
            Assert.IsNotNull(asset, "MainMenu.uxml must be importable.");
            rootVisual = asset.CloneTree();
            menu.Bind(rootVisual);
            return menu;
        }

        sealed class StubCloudSlotSource : ICloudSlotSource
        {
            readonly IReadOnlyList<CloudSlotSummary> slots;
            public CloudSlotDownloadResult DownloadResult = CloudSlotDownloadResult.Offline("No download configured.");

            public StubCloudSlotSource(params CloudSlotSummary[] slots)
            {
                this.slots = slots;
            }

            public Task<CloudSlotListResult> ListSlots(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(CloudSlotListResult.Succeeded(slots));
            }

            public Task<CloudSlotDownloadResult> DownloadSlot(int slotId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(DownloadResult);
            }
        }

        static CloudSlotDownload CloudDownload(TowerSnapshotV1 snapshot, long revision)
        {
            var envelope = JsonUtility.FromJson<SaveEnvelope>(SaveSerializer.Serialize(snapshot));
            return new CloudSlotDownload
            {
                slotId = 1,
                revision = revision,
                towerName = snapshot.towerName,
                schemaVersion = snapshot.schemaVersion,
                checksum = envelope.payloadChecksum,
                payloadBase64 = envelope.payloadBase64,
                deviceName = "Studio PC",
                playMinutes = 100,
                gameVersion = "test",
                clientInstallId = "cloud-install",
                modifiedUtc = snapshot.modifiedUtc
            };
        }

        static TowerSnapshotV1 Snapshot(string saveId, string towerName, int walletBalance)
        {
            return new TowerSnapshotV1
            {
                schemaVersion = SaveSchema.CurrentVersion,
                saveId = saveId,
                towerName = towerName,
                difficulty = nameof(GameDifficulty.Hard),
                modifiedUtc = "2026-09-26T16:00:00.0000000Z",
                walletBalance = walletBalance,
                stars = 2,
                clock = new ClockSnapshotV1
                {
                    dayIndex = 17,
                    minuteOfDay = 731,
                    minuteAccumulator = 0.5f,
                    minutesPerRealSecond = 1f
                },
                research = new ResearchSnapshotV1
                {
                    completed = Array.Empty<ResearchCompletedNodeV1>(),
                    progress = Array.Empty<ResearchProgressNodeV1>(),
                    activeBranch = string.Empty,
                    activeLevel = 0,
                    paused = false
                },
                rooms = Array.Empty<RoomSnapshotV1>()
            };
        }
    }
}
