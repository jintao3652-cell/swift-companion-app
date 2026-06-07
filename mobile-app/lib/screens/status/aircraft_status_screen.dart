import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../providers/aircraft_state_provider.dart';
import '../../providers/connection_provider.dart';
import '../../providers/controllers_provider.dart';
import '../../models/controller_info.dart';
import 'dart:async';

class AircraftStatusScreen extends ConsumerStatefulWidget {
  const AircraftStatusScreen({Key? key}) : super(key: key);

  @override
  ConsumerState<AircraftStatusScreen> createState() => _AircraftStatusScreenState();
}

class _AircraftStatusScreenState extends ConsumerState<AircraftStatusScreen> {
  Timer? _pollTimer;

  @override
  void initState() {
    super.initState();
    // 启动轮询
    _startPolling();
  }

  @override
  void dispose() {
    _stopPolling();
    super.dispose();
  }

  void _startPolling() {
    // 立即拉取一次
    Future.microtask(() {
      ref.read(aircraftStateProvider.notifier).fetchState();
      ref.read(controllersProvider.notifier).fetchControllers();
    });

    // 每 5 秒轮询一次（兜底）
    _pollTimer = Timer.periodic(const Duration(seconds: 5), (_) {
      ref.read(aircraftStateProvider.notifier).fetchState();
      ref.read(controllersProvider.notifier).fetchControllers();
    });
  }

  void _stopPolling() {
    _pollTimer?.cancel();
    _pollTimer = null;
  }

  @override
  Widget build(BuildContext context) {
    final aircraftState = ref.watch(aircraftStateProvider);
    final connectionState = ref.watch(connectionProvider);
    final controllersState = ref.watch(controllersProvider);

    final isConnected = connectionState.isConnected && connectionState.swiftConnected;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Aircraft Status'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () {
              ref.read(aircraftStateProvider.notifier).fetchState();
              ref.read(controllersProvider.notifier).fetchControllers();
            },
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: () async {
          await Future.wait([
            ref.read(aircraftStateProvider.notifier).fetchState(),
            ref.read(controllersProvider.notifier).fetchControllers(),
          ]);
        },
        child: aircraftState == null
            ? _buildNoDataView(context, isConnected)
            : _buildStatusView(context, ref, aircraftState, controllersState),
      ),
    );
  }

  Widget _buildNoDataView(BuildContext context, bool isConnected) {
    return ListView(
      children: [
        SizedBox(
          height: MediaQuery.of(context).size.height * 0.6,
          child: Center(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(
                  isConnected ? Icons.flight_takeoff : Icons.cloud_off,
                  size: 80,
                  color: Colors.grey,
                ),
                const SizedBox(height: 16),
                Text(
                  isConnected
                      ? 'No aircraft data available'
                      : 'Not connected to swift',
                  style: TextStyle(
                    fontSize: 18,
                    color: Colors.grey.shade600,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  isConnected
                      ? 'Pull down to refresh'
                      : 'Check Bridge connection',
                  style: TextStyle(
                    fontSize: 14,
                    color: Colors.grey.shade500,
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildStatusView(BuildContext context, WidgetRef ref, dynamic aircraftState, ControllersState controllersState) {
    final callsign = aircraftState.callsign ?? 'Unknown';
    final altitude = aircraftState.position?.altitude ?? 0;
    final groundSpeed = aircraftState.groundSpeed ?? 0;
    final heading = aircraftState.heading ?? 0;
    final squawk = aircraftState.squawk ?? 'N/A';
    final status = aircraftState.status ?? 'N/A';
    final com1 = aircraftState.com1Frequency ?? 0;
    final onGround = aircraftState.onGround ?? false;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        // Callsign Card
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              children: [
                const Icon(Icons.flight_takeoff, size: 48),
                const SizedBox(height: 8),
                Text(
                  callsign,
                  style: Theme.of(context).textTheme.headlineMedium,
                ),
                const SizedBox(height: 4),
                Text(
                  onGround ? 'On Ground' : 'In Flight',
                  style: TextStyle(
                    color: onGround ? Colors.orange.shade700 : Colors.green.shade700,
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),

        // Flight Data (HDG, GS, ALT, SQK)
        _buildSectionTitle(context, 'Flight Data'),
        Card(
          child: Column(
            children: [
              _buildInfoTile(
                icon: Icons.explore,
                label: 'Heading',
                value: '${heading}°',
              ),
              const Divider(height: 1),
              _buildInfoTile(
                icon: Icons.speed,
                label: 'Ground Speed',
                value: '$groundSpeed kts',
              ),
              const Divider(height: 1),
              _buildInfoTile(
                icon: Icons.height,
                label: 'Altitude',
                value: '$altitude ft',
              ),
              const Divider(height: 1),
              _buildInfoTile(
                icon: Icons.numbers,
                label: 'Squawk',
                value: squawk,
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),

        // Aircraft Status
        _buildSectionTitle(context, 'Aircraft Status'),
        Card(
          child: _buildInfoTile(
            icon: Icons.info_outline,
            label: 'Status',
            value: _formatStatus(status),
          ),
        ),
        const SizedBox(height: 16),

        // Radio Frequency
        _buildSectionTitle(context, 'Radio Frequency'),
        Card(
          child: _buildInfoTile(
            icon: Icons.radio,
            label: 'COM1',
            value: _formatFrequency(com1),
          ),
        ),
        const SizedBox(height: 16),

        // Controllers Section
        _buildSectionTitle(context, 'Online Controllers'),
        _buildControllersCard(context, controllersState),
      ],
    );
  }

  Widget _buildControllersCard(BuildContext context, ControllersState state) {
    if (state.isLoading && state.controllers.isEmpty) {
      return const Card(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Center(child: CircularProgressIndicator()),
        ),
      );
    }

    if (state.error != null) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              const Icon(Icons.error_outline, color: Colors.orange),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  'Unable to load controllers',
                  style: TextStyle(color: Colors.grey.shade700),
                ),
              ),
            ],
          ),
        ),
      );
    }

    if (state.controllers.isEmpty) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              const Icon(Icons.cell_tower_outlined, color: Colors.grey),
              const SizedBox(width: 12),
              Text(
                'No controllers nearby',
                style: TextStyle(color: Colors.grey.shade600),
              ),
            ],
          ),
        ),
      );
    }

    // 按类型分组
    final grouped = state.groupedControllers;
    final typeOrder = ['TOWER', 'GROUND', 'DELIVERY', 'APPROACH', 'DEPARTURE', 'CENTER', 'FSS', 'ATIS', 'OTHER'];

    return Card(
      child: Column(
        children: [
          for (var type in typeOrder)
            if (grouped.containsKey(type) && grouped[type]!.isNotEmpty)
              ..._buildControllerGroup(context, type, grouped[type]!),
        ],
      ),
    );
  }

  List<Widget> _buildControllerGroup(BuildContext context, String type, List<ControllerInfo> controllers) {
    return [
      Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        color: Colors.grey.shade100,
        child: Text(
          type,
          style: TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.bold,
            color: Colors.grey.shade700,
          ),
        ),
      ),
      ...controllers.map((controller) => _buildControllerTile(context, controller)),
    ];
  }

  Widget _buildControllerTile(BuildContext context, ControllerInfo controller) {
    return ListTile(
      leading: const Icon(Icons.headset_mic, size: 24),
      title: Text(controller.callsign),
      subtitle: controller.atis != null && controller.atis!.isNotEmpty
          ? Text(
              controller.atis!,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: 12, color: Colors.grey.shade600),
            )
          : null,
      trailing: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(
          color: Colors.blue.shade50,
          borderRadius: BorderRadius.circular(12),
        ),
        child: Text(
          controller.frequency,
          style: TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.bold,
            color: Colors.blue.shade700,
          ),
        ),
      ),
    );
  }

  Widget _buildSectionTitle(BuildContext context, String title) {
    return Padding(
      padding: const EdgeInsets.only(left: 4, bottom: 8),
      child: Text(
        title,
        style: Theme.of(context).textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.bold,
            ),
      ),
    );
  }

  Widget _buildInfoTile({
    required IconData icon,
    required String label,
    required String value,
    Widget? trailing,
  }) {
    return ListTile(
      leading: Icon(icon, size: 24),
      title: Text(label),
      trailing: trailing ??
          Text(
            value,
            style: const TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.bold,
            ),
          ),
    );
  }

  String _formatFrequency(int frequency) {
    if (frequency == 0) return 'N/A';
    final freqStr = frequency.toString().padLeft(6, '0');
    return '${freqStr.substring(0, 3)}.${freqStr.substring(3)}';
  }

  String _formatStatus(String status) {
    switch (status.toLowerCase()) {
      case 'atgate':
        return 'At Gate / Parked';
      case 'taxiing':
        return 'Taxiing';
      case 'climbing':
        return 'Climbing';
      case 'cruising':
        return 'Cruising';
      case 'descending':
        return 'Descending';
      case 'approaching':
        return 'Approaching';
      case 'arrived':
        return 'Landed / Arrived';
      case 'enroute':
        return 'En Route';
      case 'n/a':
        return 'N/A';
      default:
        return status;
    }
  }
}

