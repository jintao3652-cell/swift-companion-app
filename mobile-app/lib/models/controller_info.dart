class ControllerInfo {
  final String callsign;
  final String frequency;
  final String type;
  final String? atis;

  ControllerInfo({
    required this.callsign,
    required this.frequency,
    required this.type,
    this.atis,
  });

  factory ControllerInfo.fromJson(Map<String, dynamic> json) {
    return ControllerInfo(
      callsign: json['callsign'] as String,
      frequency: json['frequency'] as String,
      type: json['type'] as String,
      atis: json['atis'] as String?,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'callsign': callsign,
      'frequency': frequency,
      'type': type,
      'atis': atis,
    };
  }
}

class ControllersResponse {
  final bool located;
  final List<ControllerInfo> controllers;

  ControllersResponse({
    required this.located,
    required this.controllers,
  });

  factory ControllersResponse.fromJson(Map<String, dynamic> json) {
    return ControllersResponse(
      located: json['located'] as bool,
      controllers: (json['controllers'] as List<dynamic>?)
              ?.map((e) => ControllerInfo.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
    );
  }
}
