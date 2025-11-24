using System;
using CatCore.Services.Multiplexer;

namespace SaberSurgeon.Events
{
    /// <summary>
    /// Event arguments for login events
    /// </summary>
    public class LoginEventArgs : EventArgs
    {
        public MultiplexedPlatformService ChatService { get; private set; }

        public LoginEventArgs(MultiplexedPlatformService service)
        {
            this.ChatService = service;
        }
    }
}
