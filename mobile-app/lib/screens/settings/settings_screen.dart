import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../providers/connection_provider.dart';
import '../../services/storage_service.dart';

class SettingsScreen extends ConsumerWidget {
  const SettingsScreen({Key? key}) : super(key: key);

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final connectionState = ref.watch(connectionProvider);
    final storage = StorageService();

    return Scaffold(
      appBar: AppBar(title: const Text('Settings')),
      body: ListView(
        children: [
          ListTile(
            leading: Icon(
              connectionState.isConnected ? Icons.check_circle : Icons.error_outline,
              color: connectionState.isConnected ? Colors.green : Colors.red,
            ),
            title: Text(connectionState.isConnected ? 'Connected' : 'Disconnected'),
            subtitle: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(connectionState.bridgeAddress ?? 'No bridge'),
                if (connectionState.server != null)
                  Text('Server: ${connectionState.server}',
                      style: const TextStyle(fontSize: 12)),
              ],
            ),
          ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.refresh),
            title: const Text('Reconnect'),
            onTap: () async {
              final token = await storage.getAuthToken();
              final addr = await storage.getBridgeAddress();
              if (token != null && addr != null) {
                try {
                  await ref.read(connectionProvider.notifier).connect(addr, token);
                  if (context.mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      const SnackBar(content: Text('Reconnected')),
                    );
                  }
                } catch (e) {
                  if (context.mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(content: Text('Reconnect failed: $e')),
                    );
                  }
                }
              }
            },
          ),
          ListTile(
            leading: const Icon(Icons.link_off, color: Colors.red),
            title: const Text('Unpair / Change Address',
                style: TextStyle(color: Colors.red)),
            subtitle: const Text('Clear pairing and return to pairing screen'),
            onTap: () async {
              await ref.read(connectionProvider.notifier).disconnect();
              await storage.clearAll();
              if (context.mounted) {
                Navigator.pushNamedAndRemoveUntil(
                    context, '/pairing', (route) => false);
              }
            },
          ),
        ],
      ),
    );
  }
}
