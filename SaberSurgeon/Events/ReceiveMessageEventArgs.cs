using System;
using CatCore.Services.Multiplexer;

namespace SaberSurgeon.Events
{
    /// <summary>
    /// Event arguments for message received events
    /// </summary>
    public class ReceiveMessageEventArgs : EventArgs
    {
        public MultiplexedPlatformService ChatService { get; private set; }
        public MultiplexedMessage ChatMessage { get; private set; }

        public ReceiveMessageEventArgs(MultiplexedPlatformService service, MultiplexedMessage chatMessage)
        {
            this.ChatService = service;
            this.ChatMessage = chatMessage;
        }
    }
}
