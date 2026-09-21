/// 雷达飞机模型（对应 Bridge NearbyAircraftDto）
class RadarAircraft {
  final String callsign;
  final double latitude;
  final double longitude;
  final int altitudeFt;
  final int headingDeg;
  final int groundSpeedKt;
  final double distanceNm;

  RadarAircraft({
    required this.callsign,
    required this.latitude,
    required this.longitude,
    required this.altitudeFt,
    required this.headingDeg,
    required this.groundSpeedKt,
    this.distanceNm = 0,
  });

  factory RadarAircraft.fromJson(Map<String, dynamic> json) {
    final pos = json['position'] as Map<String, dynamic>? ?? {};
    return RadarAircraft(
      callsign: (json['callsign'] ?? '') as String,
      latitude: (pos['latitude'] as num?)?.toDouble() ?? 0,
      longitude: (pos['longitude'] as num?)?.toDouble() ?? 0,
      altitudeFt: (pos['altitude'] as num?)?.toInt() ?? 0,
      headingDeg: (json['heading'] as num?)?.toInt() ?? 0,
      groundSpeedKt: (json['groundSpeed'] as num?)?.toInt() ?? 0,
      distanceNm: (json['distanceNm'] as num?)?.toDouble() ?? 0,
    );
  }
}

/// ATC 台站模型（对应 Bridge AtcListDto）
class AtcStation {
  final String callsign;
  final int frequencyKhz;
  final String name;
  final String facilityType;

  AtcStation({
    required this.callsign,
    required this.frequencyKhz,
    required this.name,
    required this.facilityType,
  });

  /// 频率显示字符串，如 "118.800"
  String get frequencyText {
    final mhz = frequencyKhz / 1000.0;
    return mhz.toStringAsFixed(3);
  }

  factory AtcStation.fromJson(Map<String, dynamic> json) {
    return AtcStation(
      callsign: (json['callsign'] ?? '') as String,
      frequencyKhz: (json['frequency'] as num?)?.toInt() ?? 0,
      name: (json['name'] ?? '') as String,
      facilityType: (json['facilityType'] ?? '') as String,
    );
  }
}
