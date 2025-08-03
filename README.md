# Flight_Simulator_Client_And_Audio
基于FSD协议的供MFS2020, P3D, X-Plane的语音、连飞一体化客户端

本项目使用GPL3.0协议开源，使用请遵循开源协议相应内容！

# 项目概述
基于C#和FSD的连飞-语音一体化客户端程序，支持MSFS2020、P3D和X-Plane。主要功能包括：
- 多模拟器实时数据获取和发送
- FSD协议完整实现
- AI飞机渲染和管理
- 语音通信
- 飞行计划管理
- 实时位置同步

# 项目结构
```
client/
  ├── Models/
  │   ├── AircraftData.cs    - 通用飞机数据结构
  │   ├── FsdData.cs         - FSD协议数据结构
  │   ├── FsdProtocol.cs     - FSD协议定义
  │   └── Message.cs         - 通信消息结构
  ├── Services/
  │   ├── IFlightSimConnector.cs - 模拟器接口
  │   ├── FlightSimFactory.cs    - 模拟器工厂
  │   ├── Simulators/
  │   │   ├── Msfs2020Connector.cs - MSFS2020实现
  │   │   ├── P3DConnector.cs      - P3D实现
  │   │   └── XPlaneConnector.cs    - X-Plane实现
  │   └── FsdSessionManager.cs    - FSD会话管理
  └── UI/
      ├── MainWindow.xaml    - 主界面
      └── MainWindow.xaml.cs - 主程序逻辑
```

# 功能
可以连接MSFS2020、P3D(理论上也支持FSX，但FSX版未经测试)和X-Plane11/12，通过FSD通用协议实现连飞，同时支持实时语音通信，拥有自动COM1调频功能，优化了实时渲染和管理AI飞机，并提供精准的飞行计划管理和位置同步

