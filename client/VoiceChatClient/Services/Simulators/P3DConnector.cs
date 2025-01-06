using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using VoiceChatClient.Models;

namespace VoiceChatClient.Services.Simulators {
    public class P3DConnector : IFlightSimConnector {
        private SimConnect simConnect;
        private const int WM_USER_SIMCONNECT = 0x0402;
        private readonly Window mainWindow;
        private readonly Dictionary<string, uint> aiAircrafts = new();
        private uint nextAircraftId = 1;
        
        public bool IsConnected { get; private set; }
        public event EventHandler<AircraftData> AircraftDataReceived;

        public P3DConnector(Window mainWindow) {
            this.mainWindow = mainWindow;
            mainWindow.SourceInitialized += (s, e) => {
                var handle = new WindowInteropHelper(mainWindow).Handle;
                var source = HwndSource.FromHwnd(handle);
                source?.AddHook(WndProc);
            };
        }

        public void Connect() {
            try {
                simConnect = new SimConnect("P3DVoiceChat", mainWindow.Handle, WM_USER_SIMCONNECT, null, 0);
                RegisterDataDefinition();
                RequestData();
                IsConnected = true;
            }
            catch (COMException) {
                IsConnected = false;
                throw new Exception("无法连接到P3D，请确保模拟器正在运行");
            }
        }

        private void RegisterDataDefinition() {
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
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "VERTICAL SPEED", "feet per minute",
                SIMCONNECT_DATATYPE.FLOAT64);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "GEAR POSITION", "percent",
                SIMCONNECT_DATATYPE.FLOAT64);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "SIM ON GROUND", "bool",
                SIMCONNECT_DATATYPE.INT32);

            simConnect.RegisterDataDefineStruct<AircraftData>(DEFINITIONS.AircraftData);
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
                    Console.WriteLine($"P3D ReceiveMessage error: {ex.Message}");
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
            var data = new AircraftData();
            if (simConnect != null && IsConnected) {
                try {
                    simConnect.RequestDataOnSimObject(
                        REQUEST.AircraftData,
                        DEFINITIONS.AircraftData,
                        SimConnect.SIMCONNECT_OBJECT_ID_USER,
                        SIMCONNECT_PERIOD.ONCE,
                        SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                        0, 0, 0);

                    System.Threading.Thread.Sleep(50);
                    simConnect.ReceiveMessage();
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error getting P3D aircraft data: {ex.Message}");
                }
            }
            return data;
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

                    aiAircrafts.Add(data.Callsign, aircraftId);
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
                Console.WriteLine($"Error updating P3D AI aircraft: {ex.Message}");
            }
        }

        public void RemoveAIAircraft(string callsign) {
            if (aiAircrafts.TryGetValue(callsign, out uint aircraftId)) {
                try {
                    simConnect.AIRemoveObject(aircraftId, REQUEST.RemoveAircraft);
                    aiAircrafts.Remove(callsign);
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error removing P3D AI aircraft: {ex.Message}");
                }
            }
        }

        private enum DEFINITIONS {
            AircraftData
        }

        private enum REQUEST {
            AircraftData,
            RemoveAircraft
        }
    }
} 