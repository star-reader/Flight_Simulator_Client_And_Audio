using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VoiceChatClient.Models;

namespace VoiceChatClient.Services.Simulators {
    public class XPlaneConnector : IFlightSimConnector {
        private readonly UdpClient udpClient;
        private readonly UdpClient sendClient;
        private readonly IPEndPoint xplaneEndPoint;
        private readonly CancellationTokenSource cts;
        private readonly Dictionary<string, DateTime> aiAircrafts;
        private AircraftData currentData;
        private const int COM1_DATAREF = 25; // X-Plane的COM1频率数据引用ID
        
        public bool IsConnected { get; private set; }
        public event EventHandler<AircraftData> AircraftDataReceived;
        public event EventHandler<double> Com1FrequencyChanged;

        public XPlaneConnector() {
            udpClient = new UdpClient(49001); // 接收端口
            sendClient = new UdpClient();
            xplaneEndPoint = new IPEndPoint(IPAddress.Loopback, 49000);
            cts = new CancellationTokenSource();
            aiAircrafts = new Dictionary<string, DateTime>();
            currentData = new AircraftData();
        }

        public void Connect() {
            try {
                // 发送RPOS命令订阅位置数据
                byte[] rposCmd = Encoding.ASCII.GetBytes("RPOS");
                sendClient.Send(rposCmd, rposCmd.Length, xplaneEndPoint);
                
                StartDataReceiving();
                IsConnected = true;
            }
            catch (Exception) {
                IsConnected = false;
                throw new Exception("无法连接到X-Plane，请确保模拟器正在运行");
            }
        }

        private async void StartDataReceiving() {
            try {
                while (!cts.Token.IsCancellationRequested) {
                    var result = await udpClient.ReceiveAsync();
                    ProcessXPlaneData(result.Buffer);
                }
            }
            catch (Exception) {
                IsConnected = false;
            }
        }

        private void ProcessXPlaneData(byte[] data) {
            try {
                // X-Plane的数据格式: DATA\0 + 数据
                if (data.Length < 5 || Encoding.ASCII.GetString(data, 0, 4) != "DATA") 
                    return;

                int index = 5;
                // 解析位置数据
                if (data.Length >= index + 24) {
                    currentData.Latitude = BitConverter.ToSingle(data, index);
                    currentData.Longitude = BitConverter.ToSingle(data, index + 4);
                    currentData.Altitude = BitConverter.ToSingle(data, index + 8) * 3.28084f; // 转换为英尺
                    currentData.Heading = BitConverter.ToSingle(data, index + 12);
                    currentData.GroundSpeed = BitConverter.ToSingle(data, index + 16) * 1.94384f; // 转换为节
                    currentData.VerticalSpeed = BitConverter.ToSingle(data, index + 20) * 196.85f; // 转换为fpm

                    AircraftDataReceived?.Invoke(this, currentData);
                }

                // 检查是否包含COM1频率数据
                if (data.Length >= index + 29 && data[index + 24] == COM1_DATAREF) {
                    var frequency = BitConverter.ToSingle(data, index + 25);
                    Com1FrequencyChanged?.Invoke(this, frequency);
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Error processing X-Plane data: {ex.Message}");
            }
        }

        public void Disconnect() {
            cts.Cancel();
            foreach (var aircraft in aiAircrafts.Keys.ToList()) {
                RemoveAIAircraft(aircraft);
            }
            udpClient.Close();
            sendClient.Close();
            IsConnected = false;
        }

        public AircraftData GetCurrentAircraftData() {
            return currentData;
        }

        public void CreateOrUpdateAIAircraft(FsdData data) {
            if (!IsConnected) return;

            try {
                // X-Plane的多人机命令格式
                var cmd = $"PLNE{data.Callsign},{data.AircraftType},{data.Latitude:F6},{data.Longitude:F6}," +
                         $"{data.Altitude:F0},{data.Heading:F0},{data.GroundSpeed:F0}";
                
                var cmdBytes = Encoding.ASCII.GetBytes(cmd);
                sendClient.Send(cmdBytes, cmdBytes.Length, xplaneEndPoint);
                
                aiAircrafts[data.Callsign] = DateTime.Now;
            }
            catch (Exception ex) {
                Console.WriteLine($"Error updating X-Plane AI aircraft: {ex.Message}");
            }
        }

        public void RemoveAIAircraft(string callsign) {
            if (!aiAircrafts.ContainsKey(callsign)) return;

            try {
                var cmd = $"REMA{callsign}";
                var cmdBytes = Encoding.ASCII.GetBytes(cmd);
                sendClient.Send(cmdBytes, cmdBytes.Length, xplaneEndPoint);
                aiAircrafts.Remove(callsign);
            }
            catch (Exception ex) {
                Console.WriteLine($"Error removing X-Plane AI aircraft: {ex.Message}");
            }
        }

        // 清理超时的AI飞机
        private void CleanupStaleAircraft() {
            var now = DateTime.Now;
            var staleAircraft = aiAircrafts
                .Where(kvp => (now - kvp.Value).TotalSeconds > 30)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var callsign in staleAircraft) {
                RemoveAIAircraft(callsign);
            }
        }

        public double GetCom1Frequency() {
            // 发送DREF命令获取COM1频率
            var cmd = $"DREF\0{COM1_DATAREF}";
            var cmdBytes = Encoding.ASCII.GetBytes(cmd);
            sendClient.Send(cmdBytes, cmdBytes.Length, xplaneEndPoint);
            
            // 等待并解析返回数据
            var result = udpClient.Receive(ref xplaneEndPoint);
            return BitConverter.ToSingle(result, 5); // 跳过头部5字节
        }
        
        public void SetCom1Frequency(double frequency) {
            // 发送DREF命令设置COM1频率
            var cmd = $"DREF\0{COM1_DATAREF}\0{frequency}";
            var cmdBytes = Encoding.ASCII.GetBytes(cmd);
            sendClient.Send(cmdBytes, cmdBytes.Length, xplaneEndPoint);
        }
    }
} 