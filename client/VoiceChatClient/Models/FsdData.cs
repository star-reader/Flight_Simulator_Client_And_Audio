namespace VoiceChatClient.Models {
    public class FsdData {
        // 基本信息
        public string Callsign { get; set; }
        public string ClientType { get; set; } = FsdProtocol.CLIENT_PILOT;
        public string AircraftType { get; set; }
        public string AircraftCategory { get; set; } = FsdProtocol.AIRCRAFT_MEDIUM;
        
        // 位置信息
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double GroundSpeed { get; set; }
        public double Heading { get; set; }
        public double VerticalSpeed { get; set; }
        
        // 飞机状态
        public bool OnGround { get; set; }
        public string Squawk { get; set; }
        public int GearPosition { get; set; }
        public int FlapsPosition { get; set; }
        
        // 飞行计划
        public string FlightRules { get; set; } = FsdProtocol.FLIGHT_RULES_IFR;
        public string DepartureAirport { get; set; }
        public string ArrivalAirport { get; set; }
        public string Alternate { get; set; }
        public string Route { get; set; }
        public string Remarks { get; set; }
        public string Equipment { get; set; }

        // 转换方法
        public string ToFsdString(string type) {
            switch (type) {
                case FsdProtocol.PILOT_POS:
                    return $"@{Callsign}:{Callsign}:P:{AircraftType}:{ClientType}:{Latitude:F6}:{Longitude:F6}:{Altitude:F0}:{GroundSpeed:F0}:{Heading:F0}:{(OnGround ? 1 : 0)}:{Squawk}";
                
                case FsdProtocol.PILOT_UPDATE:
                    return $"%{Callsign}:{ClientType}:{Latitude:F6}:{Longitude:F6}:{Altitude:F0}:{GroundSpeed:F0}:{Heading:F0}:{(OnGround ? 1 : 0)}:1";
                
                case FsdProtocol.FLIGHT_PLAN:
                    return $"#FP{Callsign}:{FlightRules}:{AircraftType}:{GroundSpeed:F0}:{DepartureAirport}:{Altitude:F0}:{ArrivalAirport}:{Remarks}:{Route}";
                
                case FsdProtocol.SQUAWK:
                    return $"#SQ{Callsign}:{Squawk}";
                
                default:
                    return string.Empty;
            }
        }

        public static FsdData FromFsdString(string fsdString) {
            try {
                if (string.IsNullOrEmpty(fsdString)) return null;

                var data = new FsdData();
                
                switch (fsdString[0]) {
                    case '@': // 完整位置数据
                        var parts = fsdString.Substring(1).Split(':');
                        if (parts.Length < 12) return null;
                        
                        data.Callsign = parts[0];
                        data.AircraftType = parts[3];
                        data.ClientType = parts[4];
                        data.Latitude = double.Parse(parts[5]);
                        data.Longitude = double.Parse(parts[6]);
                        data.Altitude = double.Parse(parts[7]);
                        data.GroundSpeed = double.Parse(parts[8]);
                        data.Heading = double.Parse(parts[9]);
                        data.OnGround = parts[10] == "1";
                        data.Squawk = parts[11];
                        break;

                    case '%': // 位置更新
                        parts = fsdString.Substring(1).Split(':');
                        if (parts.Length < 8) return null;
                        
                        data.Callsign = parts[0];
                        data.ClientType = parts[1];
                        data.Latitude = double.Parse(parts[2]);
                        data.Longitude = double.Parse(parts[3]);
                        data.Altitude = double.Parse(parts[4]);
                        data.GroundSpeed = double.Parse(parts[5]);
                        data.Heading = double.Parse(parts[6]);
                        data.OnGround = parts[7] == "1";
                        break;

                    case '#': // 其他命令
                        if (fsdString.StartsWith("#FP")) {
                            // 处理飞行计划
                            parts = fsdString.Substring(3).Split(':');
                            if (parts.Length < 9) return null;
                            
                            data.Callsign = parts[0];
                            data.FlightRules = parts[1];
                            data.AircraftType = parts[2];
                            data.GroundSpeed = double.Parse(parts[3]);
                            data.DepartureAirport = parts[4];
                            data.Altitude = double.Parse(parts[5]);
                            data.ArrivalAirport = parts[6];
                            data.Remarks = parts[7];
                            data.Route = parts[8];
                        }
                        break;
                }
                
                return data;
            }
            catch (Exception ex) {
                Console.WriteLine($"Error parsing FSD string: {ex.Message}");
                return null;
            }
        }
    }
} 