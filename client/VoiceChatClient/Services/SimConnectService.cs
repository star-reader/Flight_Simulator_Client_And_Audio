using Microsoft.FlightSimulator.SimConnect;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VoiceChatClient.Models;

namespace VoiceChatClient.Services {
    public class SimConnectService {
        private SimConnect simConnect;
        private const int WM_USER_SIMCONNECT = 0x0402;
        private readonly Window mainWindow;
        
        public event EventHandler<AircraftData> AircraftDataReceived;
        public bool IsConnected { get; private set; }

        private ConcurrentDictionary<string, uint> aiAircrafts = new ConcurrentDictionary<string, uint>();
        private uint nextAircraftId = 1;

        public SimConnectService(Window mainWindow) {
            this.mainWindow = mainWindow;
            mainWindow.SourceInitialized += (s, e) => {
                var handle = new WindowInteropHelper(mainWindow).Handle;
                var source = HwndSource.FromHwnd(handle);
                source?.AddHook(WndProc);
            };
        }

        public void Connect() {
            try {
                simConnect = new SimConnect("VoiceChat", mainWindow.Handle, WM_USER_SIMCONNECT, null, 0);
                RegisterDataDefinition();
                RequestData();
                IsConnected = true;
            }
            catch (COMException) {
                IsConnected = false;
                throw new Exception("无法连接到MSFS2020，请确保模拟器正在运行");
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
                "GEAR POSITION", "percent",
                SIMCONNECT_DATATYPE.FLOAT64);
            simConnect.AddToDataDefinition(DEFINITIONS.AircraftData,
                "FLAPS HANDLE INDEX", "number",
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
                simConnect.ReceiveMessage();
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        public void Disconnect() {
            if (simConnect != null) {
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
                    Console.WriteLine($"Error getting aircraft data: {ex.Message}");
                }
            }
            return data;
        }

        public void CreateOrUpdateAIAircraft(FsdData fsdData) {
            if (!IsConnected) return;

            try {
                if (!aiAircrafts.TryGetValue(fsdData.Callsign, out uint aircraftId)) {
                    // Create new AI aircraft
                    aircraftId = nextAircraftId++;
                    simConnect.AICreateNonATCAircraft(
                        fsdData.AircraftType,
                        fsdData.Callsign,
                        new SimConnect_Data_InitPosition {
                            Latitude = fsdData.Latitude,
                            Longitude = fsdData.Longitude,
                            Altitude = fsdData.Altitude,
                            Pitch = 0,
                            Bank = 0,
                            Heading = fsdData.Heading,
                            OnGround = 0,
                            Airspeed = fsdData.GroundSpeed
                        },
                        aircraftId);

                    aiAircrafts.TryAdd(fsdData.Callsign, aircraftId);
                }
                else {
                    // Update existing AI aircraft
                    simConnect.AICreateNonATCAircraft(
                        fsdData.AircraftType,
                        fsdData.Callsign,
                        new SimConnect_Data_InitPosition {
                            Latitude = fsdData.Latitude,
                            Longitude = fsdData.Longitude,
                            Altitude = fsdData.Altitude,
                            Pitch = 0,
                            Bank = 0,
                            Heading = fsdData.Heading,
                            OnGround = 0,
                            Airspeed = fsdData.GroundSpeed
                        },
                        aircraftId);
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Error updating AI aircraft: {ex.Message}");
            }
        }

        public void RemoveAIAircraft(string callsign) {
            if (aiAircrafts.TryRemove(callsign, out uint aircraftId)) {
                try {
                    simConnect.AIRemoveObject(aircraftId, REQUEST.RemoveAircraft);
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error removing AI aircraft: {ex.Message}");
                }
            }
        }

        private enum DEFINITIONS {
            AircraftData,
        }

        private enum REQUEST {
            AircraftData,
            RemoveAircraft
        }
    }
} 