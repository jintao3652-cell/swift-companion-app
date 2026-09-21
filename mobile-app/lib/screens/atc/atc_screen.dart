import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../models/radar_models.dart';
import '../../providers/radar_provider.dart';
import '../chat/private_chat_screen.dart';

/// ATC 列表页：在线管制台站，点击直接发起私信。
class AtcScreen extends ConsumerWidget {
  const AtcScreen({Key? key}) : super(key: key);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final radar = ref.watch(radarProvider);
    final atc = radar.atc;

    return Scaffold(
      appBar: AppBar(
        title: Text('ATC  (${atc.length})'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => ref.read(radarProvider.notifier).start(),
          ),
        ],
      ),
      body: atc.isEmpty
          ? Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.cell_tower,
                      size: 56, color: Theme.of(context).colorScheme.outline),
                  const SizedBox(height: 12),
                  const Text('No ATC online',
                      style: TextStyle(color: Colors.grey)),
                  const SizedBox(height: 4),
                  const Text('swift 未连接网络或附近无管制',
                      style: TextStyle(color: Colors.grey, fontSize: 12)),
                ],
              ),
            )
          : ListView.separated(
              padding: const EdgeInsets.all(12),
              itemCount: atc.length,
              separatorBuilder: (_, __) => const SizedBox(height: 8),
              itemBuilder: (context, i) => _AtcTile(station: atc[i]),
            ),
    );
  }
}

class _AtcTile extends StatelessWidget {
  final AtcStation station;
  const _AtcTile({required this.station});

  Color get _typeColor {
    switch (station.facilityType) {
      case 'CENTER':
        return Colors.deepOrange;
      case 'APP-DEP':
        return Colors.orange;
      case 'TOWER':
        return Colors.blue;
      case 'GROUND':
        return Colors.teal;
      case 'CLR':
        return Colors.purple;
      default:
        return Colors.blueGrey;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      child: ListTile(
        leading: CircleAvatar(
          backgroundColor: _typeColor.withOpacity(0.18),
          child: Text(
            _facilityShort,
            style: TextStyle(color: _typeColor, fontWeight: FontWeight.bold, fontSize: 11),
          ),
        ),
        title: Text(station.callsign,
            style: const TextStyle(fontWeight: FontWeight.w600)),
        subtitle: Text(
            '${station.name.isEmpty ? station.facilityType : station.name}  ·  ${station.facilityType}'),
        trailing: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Text(station.frequencyText,
                style: const TextStyle(
                    fontFamily: 'monospace',
                    fontSize: 15,
                    fontWeight: FontWeight.bold)),
            const SizedBox(height: 2),
            const Icon(Icons.chat_bubble_outline, size: 16),
          ],
        ),
        onTap: () {
          Navigator.of(context).push(MaterialPageRoute(
            builder: (_) => PrivateChatScreen(peer: station.callsign),
          ));
        },
      ),
    );
  }

  String get _facilityShort {
    switch (station.facilityType) {
      case 'CENTER':
        return 'CTR';
      case 'APP-DEP':
        return 'APP';
      case 'TOWER':
        return 'TWR';
      case 'GROUND':
        return 'GND';
      case 'CLR':
        return 'DEL';
      case 'ATIS':
        return 'ATIS';
      default:
        return station.facilityType.isEmpty
            ? '?'
            : station.facilityType.substring(0, 3);
    }
  }
}
