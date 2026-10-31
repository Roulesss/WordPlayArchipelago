using System;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using UnityEngine;

namespace WordPlayArchipelago.Archipelago
{
    public class DeathLinkHandler
    {
        private readonly ArchipelagoClient _client;
        private DeathLinkService? _service;
        private bool _isKillingPlayer;

        public bool IsDeathLinkEnabled { get; private set; }

        public DeathLinkHandler(ArchipelagoClient client)
        {
            _client = client;
        }

        public void Initialize(bool enabled)
        {
            if (_client.Session == null) return;

            IsDeathLinkEnabled = enabled;
            _service = _client.Session.CreateDeathLinkService();

            if (enabled)
            {
                _service.EnableDeathLink();
                _service.OnDeathLinkReceived += OnDeathLinkReceived;
                Plugin.Logger.LogInfo("Death Link service initialized and enabled.");
            }
            else
            {
                Plugin.Logger.LogInfo("Death Link service initialized (disabled).");
            }
        }

        public void SetDeathLink(bool enabled)
        {
            IsDeathLinkEnabled = enabled;
            if (_service == null) return;

            if (enabled)
            {
                _service.EnableDeathLink();
                _service.OnDeathLinkReceived -= OnDeathLinkReceived;
                _service.OnDeathLinkReceived += OnDeathLinkReceived;
                Plugin.Logger.LogInfo("Death Link has been turned ON.");
            }
            else
            {
                _service.OnDeathLinkReceived -= OnDeathLinkReceived;
                Plugin.Logger.LogInfo("Death Link has been turned OFF.");
            }
        }

        private void OnDeathLinkReceived(DeathLink deathLink)
        {
            Plugin.Logger.LogWarning($"[DeathLink] Received death from '{deathLink.Source}': {deathLink.Cause}");

            // Queue the death to be handled on the main Unity thread
            _client.EnqueueMainThreadAction(() =>
            {
                KillPlayerFromDeathLink(deathLink);
            });
        }

        private void KillPlayerFromDeathLink(DeathLink deathLink)
        {
            if (LogicManagerScript.Instance == null)
            {
                Plugin.Logger.LogWarning("[DeathLink] Received death link but game session is not active.");
                return;
            }

            if (_isKillingPlayer) return;

            _isKillingPlayer = true;
            try
            {
                string causeMessage = string.IsNullOrWhiteSpace(deathLink.Cause)
                    ? $"{deathLink.Source} died, taking you with them!"
                    : deathLink.Cause;

                Plugin.Instance.UI.ShowBanner($"[DEATH LINK] {causeMessage}", Color.red, 5f);

                // Drain lives to 0 and trigger game over
                LogicManagerScript.Instance.SetLives(0);
                LogicManagerScript.Instance.GameOver(noTiles: false);
            }
            finally
            {
                _isKillingPlayer = false;
            }
        }

        public void SendDeath(string? reason = null)
        {
            try
            {
                if (!IsDeathLinkEnabled || _service == null || _isKillingPlayer) return;

                string cause = reason ?? $"{_client.SlotName} ran out of plays in Word Play!";
                Plugin.Logger?.LogInfo($"[DeathLink] Sending death link: {cause}");
                _service.SendDeathLink(new DeathLink(_client.SlotName, cause));
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[DeathLink] Exception in SendDeath: {ex.Message}");
            }
        }
    }
}
