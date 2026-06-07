import 'dart:async';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../models/controller_info.dart';
import '../services/bridge_api_service.dart';

class ControllersState {
  final bool isLoading;
  final bool located;
  final List<ControllerInfo> controllers;
  final String? error;
  final DateTime? lastUpdate;

  ControllersState({
    this.isLoading = false,
    this.located = false,
    this.controllers = const [],
    this.error,
    this.lastUpdate,
  });

  ControllersState copyWith({
    bool? isLoading,
    bool? located,
    List<ControllerInfo>? controllers,
    String? error,
    DateTime? lastUpdate,
  }) {
    return ControllersState(
      isLoading: isLoading ?? this.isLoading,
      located: located ?? this.located,
      controllers: controllers ?? this.controllers,
      error: error,
      lastUpdate: lastUpdate ?? this.lastUpdate,
    );
  }

  // 按类型分组
  Map<String, List<ControllerInfo>> get groupedControllers {
    final map = <String, List<ControllerInfo>>{};
    for (var controller in controllers) {
      map.putIfAbsent(controller.type, () => []).add(controller);
    }
    return map;
  }
}

class ControllersNotifier extends StateNotifier<ControllersState> {
  final BridgeApiService _apiService;
  Timer? _pollTimer;

  ControllersNotifier(this._apiService) : super(ControllersState());

  // 启动 5 秒轮询
  void startPolling() {
    stopPolling();
    fetchControllers(); // 立即拉取一次
    _pollTimer = Timer.periodic(const Duration(seconds: 5), (_) {
      fetchControllers();
    });
  }

  void stopPolling() {
    _pollTimer?.cancel();
    _pollTimer = null;
  }

  Future<void> fetchControllers() async {
    if (state.isLoading) return; // 避免重复请求

    state = state.copyWith(isLoading: true, error: null);

    try {
      final response = await _apiService.getOnlineControllers();
      state = ControllersState(
        isLoading: false,
        located: response.located,
        controllers: response.controllers,
        lastUpdate: DateTime.now(),
      );
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
    }
  }

  @override
  void dispose() {
    stopPolling();
    super.dispose();
  }
}

final controllersProvider =
    StateNotifierProvider<ControllersNotifier, ControllersState>((ref) {
  final apiService = ref.watch(bridgeApiServiceProvider);
  return ControllersNotifier(apiService);
});
