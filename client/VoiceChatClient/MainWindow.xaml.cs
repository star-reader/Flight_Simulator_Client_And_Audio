using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WebSocketSharp;
using VoiceChatClient.Models;
using VoiceChatClient.Services;
using System.Timers;

namespace VoiceChatClient {
    public partial class MainWindow : Window {
        private WebSocket webSocket;
        private WaveIn waveIn;
        private WaveOut waveOut;
        private BufferedWaveProvider waveProvider;
        private bool isConnected = false;
        private bool isTransmitting = false;
        private IFlightSimConnector simConnector;
        private Timer fsdTimer;
        private FsdSessionManager fsdSession;
        private readonly AudioProcessor audioProcessor;

        public MainWindow() {
            InitializeComponent();
            audioProcessor = new AudioProcessor();
            InitializeAudio();
            InitializeSimConnect();
            InitializeFsdTimer();
            InitializeFsdSession();
            this.KeyDown += MainWindow_KeyDown;
            this.KeyUp += MainWindow_KeyUp;
        }

        private void InitializeAudio() {
            waveIn = new WaveIn();
            waveIn.WaveFormat = new WaveFormat(44100, 1);
            waveIn.DataAvailable += WaveIn_DataAvailable;

            waveOut = new WaveOut();
            waveProvider = new BufferedWaveProvider(new WaveFormat(44100, 1));
            waveOut.Init(waveProvider);
            waveOut.Play();
        }

        private void InitializeSimConnect() {
            var selectedSim = SimulatorComboBox.SelectedIndex switch {
                0 => SimulatorType.MSFS2020,
                1 => SimulatorType.P3D,
                2 => SimulatorType.XPlane,
                _ => SimulatorType.MSFS2020
            };

            simConnector = FlightSimFactory.CreateConnector(selectedSim, this);
            
            try {
                simConnector.Connect();
                SimConnectIndicator.Fill = Brushes.Green;
                MessageList.Items.Add($"已连接到{SimulatorComboBox.Text}");
            }
            catch (Exception ex) {
                SimConnectIndicator.Fill = Brushes.Red;
                MessageList.Items.Add($"{SimulatorComboBox.Text}连接失败: {ex.Message}");
            }

            simConnector.AircraftDataReceived += (s, data) => {
                Dispatcher.Invoke(() => {
                    UpdateFlightData(data);
                });
            };
        }

        private void InitializeFsdTimer() {
            fsdTimer = new Timer(5000); // 5秒
            fsdTimer.Elapsed += FsdTimer_Elapsed;
        }

        private void InitializeFsdSession() {
            fsdSession = new FsdSessionManager(
                "ws://your-fsd-server:port", 
                UserNameTextBox.Text,
                simConnector);
            
            AutoTuningCheckBox.Checked += (s, e) => fsdSession.ToggleAutoTuning(true);
            AutoTuningCheckBox.Unchecked += (s, e) => fsdSession.ToggleAutoTuning(false);
        }

        private void FsdTimer_Elapsed(object sender, ElapsedEventArgs e) {
            if (isConnected && simConnector.IsConnected) {
                var aircraftData = simConnector.GetCurrentAircraftData();
                var fsdData = new FsdData {
                    Callsign = UserNameTextBox.Text,
                    Latitude = aircraftData.Latitude,
                    Longitude = aircraftData.Longitude,
                    Altitude = aircraftData.Altitude,
                    Heading = aircraftData.Heading,
                    GroundSpeed = aircraftData.GroundSpeed,
                    AircraftType = "B738", // 默认使用波音737-800，实际应该从MSFS获取
                    Squawk = "7000"  // 默认应答机码
                };

                // 直接发送FSD格式字符串
                webSocket.Send(fsdData.ToFsdString());
            }
        }

        private void UpdateFlightData(AircraftData data) {
            PositionText.Text = $"{data.Latitude:F6}°, {data.Longitude:F6}°";
            AltitudeText.Text = $"{data.Altitude:F0} ft";
            HeadingText.Text = $"{data.Heading:F0}°";
            SpeedText.Text = $"{data.GroundSpeed:F0} kts";
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e) {
            if (!isConnected) {
                if (string.IsNullOrEmpty(UserNameTextBox.Text) || string.IsNullOrEmpty(FrequencyTextBox.Text)) {
                    MessageBox.Show("请输入用户名和频率!");
                    return;
                }

                webSocket = new WebSocket("ws://localhost:8080/ws");
                
                webSocket.OnMessage += (s, args) => {
                    if (args.Data.StartsWith("@")) {
                        // 这是FSD数据
                        var fsdData = FsdData.FromFsdString(args.Data);
                        if (fsdData != null && fsdData.Callsign != UserNameTextBox.Text) {
                            Dispatcher.Invoke(() => {
                                simConnector.CreateOrUpdateAIAircraft(fsdData);
                            });
                        }
                    }
                    else if (args.Data.StartsWith("#")) {
                        // 这是语音数据
                        // ... 处理语音数据 ...
                    }
                    else {
                        // 其他类型的消息
                        // ... 处理其他消息 ...
                    }
                };

                webSocket.OnOpen += (s, args) => {
                    var joinMessage = new Message {
                        Type = "join",
                        Username = UserNameTextBox.Text,
                        Frequency = FrequencyTextBox.Text
                    };
                    webSocket.Send(JsonConvert.SerializeObject(joinMessage));
                    
                    Dispatcher.Invoke(() => {
                        ConnectButton.Content = "断开";
                        isConnected = true;
                        MessageList.Items.Add($"已连接到服务器，频率: {FrequencyTextBox.Text}");
                    });
                    fsdTimer.Start();
                };

                webSocket.OnClose += (s, args) => {
                    Dispatcher.Invoke(() => {
                        ConnectButton.Content = "连接";
                        isConnected = false;
                        MessageList.Items.Add("已断开连接");
                    });
                    fsdTimer.Stop();
                };

                webSocket.OnError += (s, args) => {
                    Dispatcher.Invoke(() => {
                        MessageBox.Show($"连接错误: {args.Message}");
                    });
                };

                try {
                    webSocket.Connect();
                }
                catch (Exception ex) {
                    MessageBox.Show($"连接失败: {ex.Message}");
                }
            } else {
                fsdTimer.Stop();
                webSocket.Close();
                ConnectButton.Content = "连接";
                isConnected = false;
            }
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e) {
            if (isConnected && isTransmitting) {
                var message = new Message {
                    Type = "voice",
                    Username = UserNameTextBox.Text,
                    Frequency = FrequencyTextBox.Text,
                    Data = Convert.ToBase64String(e.Buffer)
                };
                webSocket.Send(JsonConvert.SerializeObject(message));
            }
        }

        private void StartTransmitting() {
            if (isConnected && !isTransmitting) {
                isTransmitting = true;
                TransmitIndicator.Fill = Brushes.Red;
                waveIn.StartRecording();
            }
        }

        private void StopTransmitting() {
            if (isTransmitting) {
                isTransmitting = false;
                TransmitIndicator.Fill = Brushes.Gray;
                waveIn.StopRecording();
            }
        }

        private void PttButton_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            StartTransmitting();
        }

        private void PttButton_PreviewMouseUp(object sender, MouseButtonEventArgs e) {
            StopTransmitting();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Space) {
                StartTransmitting();
            }
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Space) {
                StopTransmitting();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
            fsdTimer?.Stop();
            fsdTimer?.Dispose();
            simConnector?.Disconnect();
            if (webSocket != null && webSocket.ReadyState == WebSocketState.Open) {
                webSocket.Close();
            }
            
            if (waveIn != null) {
                waveIn.Dispose();
            }
            
            if (waveOut != null) {
                waveOut.Dispose();
            }
            
            base.OnClosing(e);
        }

        private void HandleVoiceMessage(Message message) {
            var audioData = Convert.FromBase64String(message.Data);
            audioProcessor.AddStream(message.Username, audioData);
            
            // 处理所有活跃音频流
            var processedAudio = audioProcessor.ProcessAudio();
            if (processedAudio.Length > 0) {
                waveProvider.AddSamples(processedAudio, 0, processedAudio.Length);
            }
        }
    }
} 