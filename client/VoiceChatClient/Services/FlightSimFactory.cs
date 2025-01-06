public enum SimulatorType {
    MSFS2020,
    P3D,
    XPlane
}

public static class FlightSimFactory {
    public static IFlightSimConnector CreateConnector(SimulatorType type, Window mainWindow) {
        return type switch {
            SimulatorType.MSFS2020 => new Msfs2020Connector(mainWindow),
            SimulatorType.P3D => new P3DConnector(mainWindow),
            SimulatorType.XPlane => new XPlaneConnector(),
            _ => throw new ArgumentException("Unsupported simulator type")
        };
    }
} 