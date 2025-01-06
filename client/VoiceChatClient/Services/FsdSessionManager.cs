using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using VoiceChatClient.Models;
using Newtonsoft.Json;

public class FsdSessionManager {
    private readonly WebSocket webSocket;
    private readonly Timer heartbeatTimer;
    private readonly string callsign;
    private readonly Dictionary<string, FsdData> knownAircraft;
    private readonly IFlightSimConnector simConnector;
    private double currentFrequency;
    private bool autoTuning = true;
    
    public event EventHandler<FsdData> AircraftUpdated;
    public event EventHandler<string> AircraftRemoved;
    public event EventHandler<string> MessageReceived;

    public FsdSessionManager(string serverUrl, string callsign, IFlightSimConnector simConnector) {
        this.callsign = callsign;
        this.knownAircraft = new Dictionary<string, FsdData>();
        this.simConnector = simConnector;
        
        webSocket = new WebSocket(serverUrl);
        webSocket.OnMessage += HandleMessage;
        
        heartbeatTimer = new Timer(30000); // 30秒发送一次心跳
        heartbeatTimer.Elapsed += (s, e) => SendHeartbeat();
        
        // 监听模拟器频率变化
        simConnector.Com1FrequencyChanged += (s, freq) => {
            if (autoTuning && Math.Abs(freq - currentFrequency) > 0.001) {
                // 频率变化超过0.001MHz时更新
                currentFrequency = freq;
                UpdateFrequency(freq);
            }
        };
    }

    public void Connect() {
        webSocket.Connect();
        heartbeatTimer.Start();
    }

    public void Disconnect() {
        heartbeatTimer.Stop();
        webSocket.Close();
    }

    public void SendPosition(FsdData data) {
        // 发送完整位置数据
        webSocket.Send(data.ToFsdString(FsdProtocol.PILOT_POS));
        // 发送位置更新
        webSocket.Send(data.ToFsdString(FsdProtocol.PILOT_UPDATE));
    }

    public void SendFlightPlan(FsdData data) {
        webSocket.Send(data.ToFsdString(FsdProtocol.FLIGHT_PLAN));
    }

    private void SendHeartbeat() {
        webSocket.Send($"{FsdProtocol.PING}{callsign}");
    }

    private void HandleMessage(object sender, MessageEventArgs e) {
        var message = e.Data;
        
        if (string.IsNullOrEmpty(message)) return;

        try {
            switch (message[0]) {
                case '@':
                case '%':
                    var data = FsdData.FromFsdString(message);
                    if (data != null && data.Callsign != callsign) {
                        knownAircraft[data.Callsign] = data;
                        AircraftUpdated?.Invoke(this, data);
                    }
                    break;

                case '#':
                    if (message.StartsWith(FsdProtocol.KILL)) {
                        var targetCallsign = message.Substring(3);
                        knownAircraft.Remove(targetCallsign);
                        AircraftRemoved?.Invoke(this, targetCallsign);
                    }
                    else if (message.StartsWith(FsdProtocol.TEXT_MSG)) {
                        MessageReceived?.Invoke(this, message.Substring(3));
                    }
                    break;
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error handling message: {ex.Message}");
        }
    }

    public void SetFrequency(double frequency) {
        if (autoTuning) {
            currentFrequency = frequency;
            simConnector.SetCom1Frequency(frequency);
        }
    }
    
    private void UpdateFrequency(double frequency) {
        // 发送频率更新消息
        var message = new Message {
            Type = "frequency",
            Username = callsign,
            Frequency = frequency.ToString("F3")
        };
        webSocket.Send(JsonConvert.SerializeObject(message));
    }
    
    public void ToggleAutoTuning(bool enable) {
        autoTuning = enable;
    }
} 