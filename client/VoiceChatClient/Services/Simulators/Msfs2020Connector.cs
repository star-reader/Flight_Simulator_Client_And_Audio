using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using VoiceChatClient.Models;

namespace VoiceChatClient.Services.Simulators {
    public class Msfs2020Connector : SimConnectBase, IFlightSimConnector {
        private SimConnect simConnect;
        private const int WM_USER_SIMCONNECT = 0x0402;
        private readonly Window mainWindow;
        private readonly ConcurrentDictionary<string, uint> aiAircrafts;
        private uint nextAircraftId = 1;
        private AircraftData currentData;
        
        public bool IsConnected { get; private set; }
        public event EventHandler<AircraftData> AircraftDataReceived;

        public Msfs2020Connector(Window mainWindow) {
            this.mainWindow = mainWindow;
            this.aiAircrafts = new ConcurrentDictionary<string, uint>();
            
            mainWindow.SourceInitialized += (s, e) => {
                var handle = new WindowInteropHelper(mainWindow).Handle;
                var source = HwndSource.FromHwnd(handle);
                source?.AddHook(WndProc);
            };
        }

        public void Connect() {
            try {
                simConnect = new SimConnect("MSFS2020VoiceChat", mainWindow.Handle, WM_USER_SIMCONNECT, null, 0);
                RegisterDataDefinition();
                RegisterComFrequencyDefinition(simConnect);
                RequestData();
                RequestComFrequencyData(simConnect);
                IsConnected = true;
            }
            catch (COMException) {
                IsConnected = false;
                throw new Exception("无法连接到MSFS2020，请确保模拟器正在运行");
            }
        }

        private void RegisterDataDefinition() {
            // 基础飞行数据
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "PLANE LATITUDE", "degrees",
                SIMCONNECT_DATATYPE.FLOAT64);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "PLANE LONGITUDE", "degrees",
                SIMCONNECT_DATATYPE.FLOAT64);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "PLANE ALTITUDE", "feet",
                SIMCONNECT_DATATYPE.FLOAT64);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "PLANE HEADING DEGREES TRUE", "degrees",
                SIMCONNECT_DATATYPE.FLOAT64);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "GROUND VELOCITY", "knots",
                SIMCONNECT_DATATYPE.FLOAT64);
            
            // 扩展数据
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "ATC TYPE", "string",
                SIMCONNECT_DATATYPE.STRING256);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "ATC MODEL", "string",
                SIMCONNECT_DATATYPE.STRING256);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "TRANSPONDER CODE:1", "number",
                SIMCONNECT_DATATYPE.INT32);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "VERTICAL SPEED", "feet per minute",
                SIMCONNECT_DATATYPE.FLOAT64);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "SIM ON GROUND", "bool",
                SIMCONNECT_DATATYPE.INT32);

            simConnect.RegisterDataDefineStruct<AircraftData>(DEFINITIONS.AircraftData);

            // 注册数据接收回调
            simConnect.OnRecvSimobjectData += (sender, data) => {
                if (data.dwRequestID == (uint)REQUEST.AircraftData) {
                    currentData = (AircraftData)data.dwData[0];
                    AircraftDataReceived?.Invoke(this, currentData);
                }
                else if (data.dwRequestID == (uint)REQUEST.ComFrequency) {
                    var freqData = (ComFrequencyData)data.dwData[0];
                    OnCom1FrequencyChanged(freqData.Com1Frequency);
                }
            };
        }

        private void RequestData() {
            simConnect.RequestDataOnSimObject(
                REQUEST.AircraftData,
                DEFINITIONS.AircraftData,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SECOND,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0, 0, 0);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if (msg == WM_USER_SIMCONNECT && simConnect != null) {
                try {
                    simConnect.ReceiveMessage();
                }
                catch (Exception ex) {
                    Console.WriteLine($"MSFS2020 ReceiveMessage error: {ex.Message}");
                    IsConnected = false;
                }
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        public void Disconnect() {
            if (simConnect != null) {
                // 清理所有AI飞机
                foreach (var aircraft in aiAircrafts) {
                    try {
                        simConnect.AIRemoveObject(aircraft.Value, REQUEST.RemoveAircraft);
                    }
                    catch (Exception) { }
                }
                aiAircrafts.Clear();

                simConnect.Dispose();
                simConnect = null;
                IsConnected = false;
            }
        }

        public AircraftData GetCurrentAircraftData() {
            return currentData;
        }

        public void CreateOrUpdateAIAircraft(FsdData data) {
            if (!IsConnected) return;

            try {
                if (!aiAircrafts.TryGetValue(data.Callsign, out uint aircraftId)) {
                    aircraftId = nextAircraftId++;
                    simConnect.AICreateNonATCAircraft(
                        data.AircraftType,
                        data.Callsign,
                        new SimConnect_Data_InitPosition {
                            Latitude = data.Latitude,
                            Longitude = data.Longitude,
                            Altitude = data.Altitude,
                            Pitch = 0,
                            Bank = 0,
                            Heading = data.Heading,
                            OnGround = data.OnGround ? 1 : 0,
                            Airspeed = data.GroundSpeed
                        },
                        aircraftId);

                    aiAircrafts.TryAdd(data.Callsign, aircraftId);
                }
                else {
                    simConnect.AICreateNonATCAircraft(
                        data.AircraftType,
                        data.Callsign,
                        new SimConnect_Data_InitPosition {
                            Latitude = data.Latitude,
                            Longitude = data.Longitude,
                            Altitude = data.Altitude,
                            Pitch = 0,
                            Bank = 0,
                            Heading = data.Heading,
                            OnGround = data.OnGround ? 1 : 0,
                            Airspeed = data.GroundSpeed
                        },
                        aircraftId);
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Error updating MSFS2020 AI aircraft: {ex.Message}");
            }
        }

        public void RemoveAIAircraft(string callsign) {
            if (aiAircrafts.TryRemove(callsign, out uint aircraftId)) {
                try {
                    simConnect.AIRemoveObject(aircraftId, REQUEST.RemoveAircraft);
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error removing MSFS2020 AI aircraft: {ex.Message}");
                }
            }
        }

        private enum DEFINITIONS {
            AircraftData,
            ComFrequency
        }

        private enum REQUEST {
            AircraftData,
            ComFrequency,
            RemoveAircraft
        }

        private enum EVENTS {
            SetCom1Frequency
        }

        private enum GROUP {
            ID_PRIORITY_STANDARD
        }
    }
} 
