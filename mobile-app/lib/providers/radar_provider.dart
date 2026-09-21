import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../models/radar_models.dart';
import '../services/bridge_api_service.dart';
import 'connection_provider.dart';

/// 雷达数据 provider：轮询 Bridge 的 /api/aircraft/nearby 与 /api/status/atc，
/// 本机位置实时来自 aircraftStateProvider（SignalR AircraftStateUpdated 推送）。
class RadarState {
  final List<RadarAircraft> aircraft;
  final List<AtcStation> atc;
  final bool connectedToSwift;

  const RadarState({
    this.aircraft = const [],
    this.atc = const [],
    this.connectedToSwift = false,
  });

  RadarState copyWith({
    List<RadarAircraft>? aircraft,
    List<AtcStation>? atc,
    bool? connectedToSwift,
  }) =>
      RadarState(
        aircraft: aircraft ?? this.aircraft,
        atc: atc ?? this.atc,
        connectedToSwift: connectedToSwift ?? this.connectedToSwift,
      );
}

class RadarNotifier extends StateNotifier<RadarState> {
  final Ref _ref;
  Timer? _timer;

  RadarNotifier(this._ref) : super(const RadarState());

  BridgeApiService get _api => _ref.read(connectionProvider.notifier).apiService;

  /// 连接建立后调用：开始轮询
  void start() {
    if (_timer != null && _timer!.isActive) return;
    _startPolling();
  }

  void reset() {
    _timer?.cancel();
    _timer = null;
    state = const RadarState();
  }

  void _startPolling() {
    _timer?.cancel();
    _timer = Timer.periodic(const Duration(seconds: 2), (_) => _poll());
    _poll();
  }

  Future<void> _poll() async {
    try {
      final raw = await _api.getRadarAircraft(radiusNm: 300);
      final aircraft =
          raw.map((e) => RadarAircraft.fromJson(e as Map<String, dynamic>)).toList();

      final atcRaw = await _api.getOnlineControllers();
      final atc = atcRaw
          .map((e) => AtcStation.fromJson(e as Map<String, dynamic>))
          .toList();

      if (!mounted) return;
      state = state.copyWith(aircraft: aircraft, atc: atc);
    } catch (e) {
      debugPrint('Radar poll error: $e');
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }
}

final radarProvider = StateNotifierProvider<RadarNotifier, RadarState>((ref) {
  final notifier = RadarNotifier(ref);
  // 跟随连接状态: 连上后开始轮询, 断开后停止
  ref.listen(connectionProvider, (prev, next) {
    if (next.isConnected) {
      notifier.start();
    } else {
      notifier.reset();
    }
  });
  return notifier;
});
