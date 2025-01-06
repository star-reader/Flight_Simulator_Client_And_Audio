using System;
using System.Runtime.InteropServices;
using SimConnect;

public abstract class SimConnectBase : IFlightSimConnector {
    protected void RegisterComFrequencyDefinition(SimConnect simConnect) {
        // 添加COM1频率数据定义
        simConnect.AddToDataDefinition(DEFINITIONS.ComFrequency,
            "COM ACTIVE FREQUENCY:1", "MHz",
            SIMCONNECT_DATATYPE.FLOAT64);
            
        simConnect.RegisterDataDefineStruct<ComFrequencyData>(DEFINITIONS.ComFrequency);
        
        // 注册频率变化事件
        simConnect.MapClientEventToSimEvent(EVENTS.SetCom1Frequency, "COM_RADIO_SET");
    }
    
    protected void RequestComFrequencyData(SimConnect simConnect) {
        simConnect.RequestDataOnSimObject(
            REQUEST.ComFrequency,
            DEFINITIONS.ComFrequency,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SECOND,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0, 0, 0);
    }
    
    public double GetCom1Frequency() {
        var data = new ComFrequencyData();
        return data.Com1Frequency;
    }
    
    public void SetCom1Frequency(double frequency) {
        // 设置COM1频率
        simConnect.TransmitClientEvent(
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            EVENTS.SetCom1Frequency,
            (uint)(frequency * 1000000), // 转换为Hz
            GROUP.ID_PRIORITY_STANDARD,
            SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY);
    }
    
    protected void OnCom1FrequencyChanged(double frequency) {
        Com1FrequencyChanged?.Invoke(this, frequency);
    }
    
    public event EventHandler<double> Com1FrequencyChanged;
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    protected struct ComFrequencyData {
        public double Com1Frequency;
    }
} 