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

# 模拟器连接实现
----------------
### MSFS2020/P3D (SimConnect)：
- 使用SimConnect API
- 数据定义：
  * 位置：经度、纬度、高度
  * 姿态：航向、俯仰、横滚
  * 速度：地速、垂直速度
  * 状态：起落架、襟翼、地面状态
- AI飞机管理：
  * 创建：AICreateNonATCAircraft
  * 更新：通过ID重新创建
  * 删除：AIRemoveObject
- 数据更新频率：1秒

### X-Plane (UDP)：
- 使用UDP通信（端口49000/49001）
- 数据格式：
  * 接收：DATA\0 + [位置数据]
  * 发送：PLNE[呼号],[机型],[位置数据]
- 单位转换：
  * 高度：米转英尺（×3.28084）
  * 速度：m/s转节（×1.94384）
  * 垂直速度：m/s转fpm（×196.85）
- AI飞机超时清理：30秒

### 数据流程
-----------
#### 发送流程：
1. 模拟器数据获取（IFlightSimConnector实现）
2. 数据转换（AircraftData -> FsdData）
3. FSD格式化（ToFsdString()）
4. WebSocket发送
5. 心跳维护

#### 接收流程：
1. WebSocket接收
2. 消息类型判断（@,%,#）
3. 数据解析（FromFsdString()）
4. AI飞机更新
5. 界面刷新

#### 模拟器连接：
- 连接失败重试
- 断线自动重连
- 数据超时检测

#### 网络通信：
- WebSocket断线重连
- 心跳包超时处理
- 数据格式验证

#### AI飞机：
- 创建失败重试
- 更新错误恢复
- 超时清理机制

### 性能优化
-----------
#### 数据更新：
- 位置数据：5秒
- 心跳包：30秒
- AI清理：30秒

#### 资源管理：
- AI飞机数量限制
- 内存使用监控
- UDP缓冲区管理

### 自动调频功能
--------------
#### 功能概述：
- 自动同步模拟器COM1频率
- 支持MSFS2020/P3D/X-Plane
- 双向同步（模拟器<->聊天室）
- 可手动开关自动调频
