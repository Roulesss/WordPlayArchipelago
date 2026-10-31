using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using WordPlayArchipelago.Archipelago;
using WordPlayArchipelago.Patches;

namespace WordPlayArchipelago
{
    public class ArchipelagoRunner : MonoBehaviour
    {
        private void Update()
        {
            try
            {
                Plugin.StaticClient?.Update();
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[ArchipelagoRunner] Error in Update: {ex.Message}");
            }
        }
    }

    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.archipelago.wordplay";
        public const string PluginName = "Word Play Archipelago";
        public const string PluginVersion = "0.1.0";

        public static Plugin Instance { get; private set; } = null!;
        public static new ManualLogSource Logger { get; private set; } = null!;
        public static Harmony HarmonyInstance { get; private set; } = null!;
        public static GameObject? CoreGameObject { get; private set; }

        // Persistent static instances
        public static ArchipelagoClient StaticClient { get; set; } = null!;
        public static ArchipelagoUI StaticUI { get; set; } = null!;
        public static ScreamerManager StaticScreamer { get; set; } = null!;

        // Instance properties forwarding to static so existing calls never fail
        public ArchipelagoClient Client => StaticClient;
        public ArchipelagoUI UI => StaticUI;
        public ScreamerManager Screamer => StaticScreamer;

        // Configuration
        public ConfigEntry<string> ServerAddress { get; private set; } = null!;
        public ConfigEntry<int> ServerPort { get; private set; } = null!;
        public ConfigEntry<string> SlotName { get; private set; } = null!;
        public ConfigEntry<string> Password { get; private set; } = null!;
        public ConfigEntry<bool> EnableDeathLink { get; private set; } = null!;

        public static void EnsureCoreGameObject()
        {
            try
            {
                if (CoreGameObject == null)
                {
                    CoreGameObject = new GameObject("WordPlayArchipelago_Core");
                    CoreGameObject.hideFlags = HideFlags.HideAndDontSave;
                    UnityEngine.Object.DontDestroyOnLoad(CoreGameObject);

                    if (StaticClient == null)
                    {
                        StaticClient = new ArchipelagoClient();
                    }

                    StaticUI = CoreGameObject.GetComponent<ArchipelagoUI>() ?? CoreGameObject.AddComponent<ArchipelagoUI>();
                    StaticScreamer = CoreGameObject.GetComponent<ScreamerManager>() ?? CoreGameObject.AddComponent<ScreamerManager>();
                    if (CoreGameObject.GetComponent<ArchipelagoRunner>() == null)
                    {
                        CoreGameObject.AddComponent<ArchipelagoRunner>();
                    }

                    Logger?.LogInfo("[Plugin] EnsureCoreGameObject: Successfully created persistent Core GameObject and components!");
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError($"[Plugin] Error in EnsureCoreGameObject: {ex}");
            }
        }

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;

            // Bind configurations
            ServerAddress = Config.Bind("Archipelago", "ServerAddress", "archipelago.gg", "The IP or hostname of the Archipelago server.");
            ServerPort = Config.Bind("Archipelago", "ServerPort", 38281, "The port number of the Archipelago server.");
            SlotName = Config.Bind("Archipelago", "SlotName", "Player1", "Your slot name configured in the multiworld.");
            Password = Config.Bind("Archipelago", "Password", "", "The password for the room, if any.");
            EnableDeathLink = Config.Bind("Archipelago", "DeathLink", false, "Enable Death Link. If you lose a run, everyone dies, and vice versa.");

            EnsureCoreGameObject();

            // Apply Harmony patches across the entire assembly
            HarmonyInstance = new Harmony(PluginGUID);
            try
            {
                HarmonyInstance.PatchAll(typeof(Plugin).Assembly);

                foreach (var m in HarmonyInstance.GetPatchedMethods())
                {
                    Logger.LogInfo($"[Harmony] Patched method: {m.DeclaringType?.Name}.{m.Name}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Harmony] Error during PatchAll: {ex}");
            }

            // Listen for scene load events
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
            {
                Logger.LogInfo($"[SceneManager] Scene Loaded: '{scene.name}'");

                // Guarantee Core GameObject is alive on every scene transition
                EnsureCoreGameObject();

                if (scene.name == "Title")
                {
                    ArchipelagoClient.IsArchipelagoRun = false;
                    Logger.LogInfo("[SceneManager] Title scene loaded -> Reset IsArchipelagoRun = false.");
                }
                else if (scene.name == "Game")
                {
                    Logger.LogInfo($"[SceneManager] Game Scene Loaded! IsArchipelagoRun = {ArchipelagoClient.IsArchipelagoRun}, IsConnected = {StaticClient?.IsConnected}");

                    if (ArchipelagoClient.IsArchipelagoRun && StaticClient != null && StaticClient.IsConnected)
                    {
                        var controllerGo = new GameObject("ArchipelagoGameController");
                        controllerGo.AddComponent<ArchipelagoGameController>();
                        Logger.LogInfo("[SceneManager] Attached ArchipelagoGameController to Game scene!");
                    }
                }

                foreach (var ls in FindObjectsOfType<LevelSelectScript>(true))
                {
                    if (ls.gameObject.activeInHierarchy)
                    {
                        TitleLevelSelectPatches.OnLevelSelectStart(ls);
                    }
                }
            };

            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded successfully!");
        }

        private void Update()
        {
            StaticClient?.Update();
        }

        private void OnDestroy()
        {
            Logger?.LogWarning($"[Plugin] BaseUnityPlugin.OnDestroy invoked on '{gameObject.name}'. CoreGameObject is {(CoreGameObject != null ? "alive" : "null")}.");
        }
    }
}
