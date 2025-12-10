using IPA.Config.Stores;

namespace SaberSurgeon
{
    // Must be public or internal with public virtual properties for BSIPA Generated<T>()
    public class PluginConfig
    {
        public static PluginConfig Instance { get; set; }

        // --- Commands / Toggles ---

        // Bomb command keyword (without leading '!')
        public virtual string BombCommandName { get; set; } = "bomb";

        // Command toggles
        public virtual bool RainbowEnabled { get; set; } = true;
        public virtual bool DisappearEnabled { get; set; } = true;
        public virtual bool GhostEnabled { get; set; } = true;
        public virtual bool BombEnabled { get; set; } = true;
        public virtual bool FasterEnabled { get; set; } = false;
        public virtual bool SuperFastEnabled { get; set; } = false;
        public virtual bool SlowerEnabled { get; set; } = true;
        public virtual bool FlashbangEnabled { get; set; } = true;

        // Global + per‑command cooldowns
        public virtual bool GlobalCooldownEnabled { get; set; } = true;
        public virtual bool PerCommandCooldownsEnabled { get; set; } = false;
        public virtual float GlobalCooldownSeconds { get; set; } = 60f;

        public virtual float RainbowCooldownSeconds { get; set; } = 60f;
        public virtual float DisappearCooldownSeconds { get; set; } = 60f;
        public virtual float GhostCooldownSeconds { get; set; } = 60f;
        public virtual float BombCooldownSeconds { get; set; } = 60f;
        public virtual float FasterCooldownSeconds { get; set; } = 60f;
        public virtual float SuperFastCooldownSeconds { get; set; } = 60f;
        public virtual float SlowerCooldownSeconds { get; set; } = 60f;
        public virtual float FlashbangCooldownSeconds { get; set; } = 60f;

        // Only one speed effect at a time
        public virtual bool SpeedExclusiveEnabled { get; set; } = true;

        // --- OAuth Token Storage (Encrypted, used by TwitchAuthManager) ---

        public virtual string EncryptedAccessToken { get; set; } = "";
        public virtual string EncryptedRefreshToken { get; set; } = "";
        public virtual long TokenExpiryTicks { get; set; } = 0; // DateTime.Ticks

        // --- Twitch / Backend Settings ---

        // Cached info about the currently linked Twitch user (broadcaster)
        // Used by TwitchApiClient and TwitchEventClient
        public virtual string CachedBroadcasterId { get; set; } = "";
        public virtual string CachedBroadcasterLogin { get; set; } = "";

        // Supporter cache: subscription tier to phoenixblaze0
        public virtual int CachedSupporterTier { get; set; } = 0;

        // Backend selection and status
        public virtual bool PreferNativeTwitchBackend { get; set; } = true;   // Use your WebSocket backend first
        public virtual bool AllowChatPlexFallback { get; set; } = true;   // Fall back to ChatPlex if native fails
        public virtual string BackendStatus { get; set; } = "";     // For UI/debugging

        // WebSocket endpoint for your server (no channel_id here; added in TwitchEventClient)
        public virtual string EventServerUrl { get; set; } =
        "ws://phoenixblaze0.duckdns.org:42069/ws";
    }
}