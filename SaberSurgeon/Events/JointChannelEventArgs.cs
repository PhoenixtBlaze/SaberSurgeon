using System;
using CatCore.Services.Multiplexer;

namespace SaberSurgeon.Events
{
    /// <summary>
    /// Event arguments for join channel events
    /// </summary>
    public class JoinChannelEventArgs : EventArgs
    {
        public MultiplexedPlatformService ChatService { get; private set; }
        public MultiplexedChannel ChatChannel { get; private set; }

        public JoinChannelEventArgs(MultiplexedPlatformService service, MultiplexedChannel chatChannel)
        {
            this.ChatService = service;
            this.ChatChannel = chatChannel;
        }
    }
}
