import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../models/aircraft_state.dart';
import '../providers/connection_provider.dart';

/// Aircraft state provider
final aircraftStateProvider = StateNotifierProvider<AircraftStateNotifier, AircraftState?>((ref) {
  final connectionNotifier = ref.watch(connectionProvider.notifier);
  return AircraftStateNotifier(connectionNotifier);
});

class AircraftStateNotifier extends StateNotifier<AircraftState?> {
  AircraftStateNotifier(this._connectionNotifier) : super(null) {
    _initialize();
  }

  final ConnectionNotifier _connectionNotifier;

  void _initialize() {
    // Setup WebSocket aircraft state listener
    _connectionNotifier.wsService.onAircraftStateUpdated = (aircraftState) {
      updateState(aircraftState);
    };
  }

  void updateState(AircraftState newState) {
    state = newState;

    // Update connection provider with callsign
    _connectionNotifier.updateCallsign(newState.callsign);
  }

  Future<void> requestUpdate() async {
    try {
      await _connectionNotifier.wsService.requestAircraftState();
    } catch (e) {
      print('Error requesting aircraft state: $e');
    }
  }

  Future<void> fetchState() async {
    try {
      final response = await _connectionNotifier.apiService.getAircraftState();
      final aircraftState = AircraftState.fromJson(response);
      updateState(aircraftState);
    } catch (e) {
      print('Error fetching aircraft state: $e');
    }
  }
}
