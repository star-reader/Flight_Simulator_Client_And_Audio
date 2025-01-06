namespace VoiceChatClient.Models {
    public static class FsdProtocol {
        // 数据包类型
        public const string PILOT_POS = "@";      // 飞行员位置
        public const string PILOT_UPDATE = "%";    // 位置更新
        public const string ATC_POS = "$";        // ATC位置
        public const string FLIGHT_PLAN = "#FP";   // 飞行计划
        public const string METAR = "#WX";        // 气象数据
        public const string PING = "#PI";         // 心跳包
        public const string PONG = "#PO";         // 心跳响应
        public const string KILL = "#KL";         // 断开连接
        public const string TEXT_MSG = "#TM";     // 文字消息
        public const string RADIO_MSG = "#RT";    // 无线电消息
        public const string HANDOFF = "#HO";      // 移交控制
        public const string SQUAWK = "#SQ";       // 应答机设置

        // 客户端类型
        public const string CLIENT_PILOT = "PILOT";
        public const string CLIENT_ATC = "ATC";
        public const string CLIENT_OBS = "OBS";

        // 飞行规则
        public const string FLIGHT_RULES_VFR = "V";
        public const string FLIGHT_RULES_IFR = "I";

        // 飞机类型
        public const string AIRCRAFT_LIGHT = "L";  // 轻型
        public const string AIRCRAFT_MEDIUM = "M"; // 中型
        public const string AIRCRAFT_HEAVY = "H";  // 重型
    }
} 