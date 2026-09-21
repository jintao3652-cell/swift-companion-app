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
    final frequency = json['frequency'];
    String formattedFreq;
    if (frequency is int) {
      final freqStr = frequency.toString().padLeft(6, '0');
      formattedFreq = '${freqStr.substring(0, 3)}.${freqStr.substring(3)}';
    } else {
      formattedFreq = frequency.toString();
    }

    String type = 'Unknown';
    final callsign = (json['callsign'] as String).toUpperCase();
    if (callsign.contains('_TWR')) {
      type = 'Tower';
    } else if (callsign.contains('_GND')) {
      type = 'Ground';
    } else if (callsign.contains('_DEL')) {
      type = 'Delivery';
    } else if (callsign.contains('_APP') || callsign.contains('_DEP')) {
      type = 'Approach/Departure';
    } else if (callsign.contains('_CTR')) {
      type = 'Center';
    }

    return ControllerInfo(
      callsign: callsign,
      frequency: formattedFreq,
      type: type,
      atis: json['name'],
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
