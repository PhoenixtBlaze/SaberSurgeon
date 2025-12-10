using System;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaberSurgeon.Chat;
using UnityEngine;
using WebSocketSharp;

namespace SaberSurgeon.Twitch
{
    /// <summary>
    /// Lightweight WebSocket client that connects to the SaberSurgeon backend
    /// and receives Twitch events (chat, follow, sub, raid) for a single channel.
    /// </summary>
    public class TwitchEventClient
    {
        private readonly string _baseUrl;
        private readonly string _channelId;
        private WebSocket _ws;

        public bool IsConnected => _ws != null && _ws.ReadyState == WebSocketState.Open;

        // Events back to ChatManager
        public event Action<ChatContext> OnChatMessage;
        public event Action<string, int> OnSubscription;   // (user, tier)
        public event Action<string> OnFollow;              // (user)
        public event Action<string, int> OnRaid;           // (raider, viewers)

        /// <summary>
        /// baseUrl: e.g. "wss://sabersurgeon.duckdns.org:42069/ws"
        /// channelId: Twitch broadcaster_id (from Helix /users, cached in Plugin.Settings.CachedBroadcasterId)
        /// </summary>
        public TwitchEventClient(string baseUrl, string channelId)
        {
            _baseUrl = baseUrl;
            _channelId = channelId;
        }

        /// <summary>
        /// Connect asynchronously to {baseUrl}?channel_id={channelId}.
        /// Used as a Unity coroutine from ChatManager.
        /// </summary>
        public IEnumerator ConnectCoroutine()
        {
            if (string.IsNullOrEmpty(_baseUrl))
            {
                Plugin.Log.Warn("TwitchEventClient: No base URL provided, cannot connect.");
                yield break;
            }

            if (string.IsNullOrEmpty(_channelId))
            {
                Plugin.Log.Warn("TwitchEventClient: No channel_id specified, cannot connect.");
                yield break;
            }

            bool done = false;
            string fullUrl = $"{_baseUrl}?channel_id={_channelId}";
            Plugin.Log.Info($"TwitchEventClient: Connecting to {fullUrl}");

            _ws = new WebSocket(fullUrl);

            _ws.OnOpen += (_, __) =>
            {
                Plugin.Log.Info("TwitchEventClient: WebSocket opened");
                done = true;
            };

            _ws.OnMessage += (_, e) => HandleServerMessage(e.Data);

            _ws.OnError += (_, e) =>
            {
                Plugin.Log.Error($"TwitchEventClient error: {e.Message}");
            };

            _ws.OnClose += (_, __) =>
            {
                Plugin.Log.Info("TwitchEventClient: WebSocket closed");
            };

            _ws.ConnectAsync();

            float timeout = 5f;
            while (!done && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (!done || !IsConnected)
            {
                Plugin.Log.Warn("TwitchEventClient: Connection timed out or failed");
            }
        }

        /// <summary>
        /// Send a chat message request to the backend.
        /// Server may consume this to post to Twitch chat, or ignore if not implemented.
        /// </summary>
        public void SendChatMessage(string text)
        {
            if (!IsConnected)
                return;

            var payload = new
            {
                type = "chat",
                message = text
            };

            string json = JsonConvert.SerializeObject(payload);
            _ws.Send(json);
        }

        /// <summary>
        /// Called when the backend pushes an event as JSON.
        /// Expected shape:
        ///   { "type": "chat", "user": "...", "message": "...", "isMod": bool, "isSub": bool, "isVip": bool, "isBroadcaster": bool, "bits": int }
        ///   { "type": "sub",  "user": "...", "tier": 1|2|3 }
        ///   { "type": "follow", "user": "..." }
        ///   { "type": "raid",   "raider": "...", "viewers": int }
        /// </summary>
        private void HandleServerMessage(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var obj = JObject.Parse(json);
                string type = (string)obj["type"];

                switch (type)
                {
                    case "chat":
                        {
                            var ctx = new ChatContext
                            {
                                SenderName = (string)obj["user"] ?? "Unknown",
                                MessageText = (string)obj["message"] ?? string.Empty,
                                IsModerator = (bool?)obj["isMod"] ?? false,
                                IsSubscriber = (bool?)obj["isSub"] ?? false,
                                IsVip = (bool?)obj["isVip"] ?? false,
                                IsBroadcaster = (bool?)obj["isBroadcaster"] ?? false,
                                Bits = (int?)obj["bits"] ?? 0,
                                Source = ChatSource.NativeTwitch
                            };

                            OnChatMessage?.Invoke(ctx);
                            break;
                        }

                    case "follow":
                        {
                            string user = (string)obj["user"] ?? "Unknown";
                            OnFollow?.Invoke(user);
                            break;
                        }

                    case "sub":
                        {
                            string user = (string)obj["user"] ?? "Unknown";
                            int tier = (int?)obj["tier"] ?? 1;
                            OnSubscription?.Invoke(user, tier);
                            break;
                        }

                    case "raid":
                        {
                            string raider = (string)obj["raider"] ?? "Unknown";
                            int viewers = (int?)obj["viewers"] ?? 0;
                            OnRaid?.Invoke(raider, viewers);
                            break;
                        }

                    default:
                        Plugin.Log.Debug($"TwitchEventClient: Unknown event type '{type}'");
                        break;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"TwitchEventClient: Failed to handle message '{json}': {ex.Message}");
            }

        }
    }
}
