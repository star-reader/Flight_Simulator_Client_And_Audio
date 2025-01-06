namespace VoiceChatClient.Services {
    public interface IFlightSimConnector {
        bool IsConnected { get; }
        event EventHandler<AircraftData> AircraftDataReceived;
        
        void Connect();
        void Disconnect();
        AircraftData GetCurrentAircraftData();
        void CreateOrUpdateAIAircraft(FsdData data);
        void RemoveAIAircraft(string callsign);
        
        double GetCom1Frequency();
        void SetCom1Frequency(double frequency);
        
        event EventHandler<double> Com1FrequencyChanged;
    }
} 