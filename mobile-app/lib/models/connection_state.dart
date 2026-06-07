class ConnectionState {
  final bool isConnected;
  final String? bridgeAddress;
  final int? bridgePort;
  final String? callsign;
  final bool vPilotConnected;
  final String? server;
  final DateTime? lastPing;

  ConnectionState({
    this.isConnected = false,
    this.bridgeAddress,
    this.bridgePort,
    this.callsign,
    this.vPilotConnected = false,
    this.server,
    this.lastPing,
  });

  ConnectionState copyWith({
    bool? isConnected,
    String? bridgeAddress,
    int? bridgePort,
    String? callsign,
    bool? vPilotConnected,
    String? server,
    DateTime? lastPing,
  }) {
    return ConnectionState(
      isConnected: isConnected ?? this.isConnected,
      bridgeAddress: bridgeAddress ?? this.bridgeAddress,
      bridgePort: bridgePort ?? this.bridgePort,
      callsign: callsign ?? this.callsign,
      vPilotConnected: vPilotConnected ?? this.vPilotConnected,
      server: server ?? this.server,
      lastPing: lastPing ?? this.lastPing,
    );
  }

  factory ConnectionState.disconnected() {
    return ConnectionState(
      isConnected: false,
      vPilotConnected: false,
    );
  }

  factory ConnectionState.connected({
    required String bridgeAddress,
    required int bridgePort,
    String? callsign,
    bool vPilotConnected = false,
    String? server,
  }) {
    return ConnectionState(
      isConnected: true,
      bridgeAddress: bridgeAddress,
      bridgePort: bridgePort,
      callsign: callsign,
      vPilotConnected: vPilotConnected,
      server: server,
      lastPing: DateTime.now(),
    );
  }
}
