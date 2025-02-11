﻿using FrameWork;

namespace LobbyServer
{
    [aConfigAttributes("Configs/Lobby.xml")]
    public class LobbyConfigs : aConfig
    {
        public int ClientPort = 8048;
        public string ClientVersion = "1.4.8";
        public bool SeverOnFinish = true;

        public RpcClientConfig RpcInfo = new RpcClientConfig("127.0.0.1", "127.0.0.1", 6800);
        public LogInfo LogLevel = new LogInfo();
    }
}