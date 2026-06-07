class AircraftState {
  final String callsign;
  final Position position;
  final int heading;
  final int groundSpeed;
  final int? verticalSpeed;
  final String? squawk;
  final String? status;
  final int? com1Frequency;
  final int? com2Frequency;
  final bool onGround;
  final DateTime timestamp;

  AircraftState({
    required this.callsign,
    required this.position,
    required this.heading,
    required this.groundSpeed,
    this.verticalSpeed,
    this.squawk,
    this.status,
    this.com1Frequency,
    this.com2Frequency,
    this.onGround = false,
    required this.timestamp,
  });

  factory AircraftState.fromJson(Map<String, dynamic> json) {
    return AircraftState(
      callsign: json['callsign'] ?? '',
      position: Position.fromJson(json['position'] ?? {}),
      heading: json['heading'] ?? 0,
      groundSpeed: json['groundSpeed'] ?? 0,
      verticalSpeed: json['verticalSpeed'],
      squawk: json['squawk'],
      status: json['status'],
      com1Frequency: json['com1Frequency'],
      com2Frequency: json['com2Frequency'],
      onGround: json['onGround'] ?? false,
      timestamp: DateTime.parse(json['timestamp'] ?? DateTime.now().toIso8601String()),
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'callsign': callsign,
      'position': position.toJson(),
      'heading': heading,
      'groundSpeed': groundSpeed,
      'verticalSpeed': verticalSpeed,
      'squawk': squawk,
      'status': status,
      'com1Frequency': com1Frequency,
      'com2Frequency': com2Frequency,
      'onGround': onGround,
      'timestamp': timestamp.toIso8601String(),
    };
  }
}

class Position {
  final double latitude;
  final double longitude;
  final int altitude;

  Position({
    required this.latitude,
    required this.longitude,
    required this.altitude,
  });

  factory Position.fromJson(Map<String, dynamic> json) {
    return Position(
      latitude: (json['latitude'] ?? 0).toDouble(),
      longitude: (json['longitude'] ?? 0).toDouble(),
      altitude: json['altitude'] ?? 0,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'latitude': latitude,
      'longitude': longitude,
      'altitude': altitude,
    };
  }
}
