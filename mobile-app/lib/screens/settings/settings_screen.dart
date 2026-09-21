import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../providers/connection_provider.dart';
import '../../services/storage_service.dart';
import '../../services/bridge_installer_service.dart';

class SettingsScreen extends ConsumerStatefulWidget {
  const SettingsScreen({Key? key}) : super(key: key);

  @override
  ConsumerState<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends ConsumerState<SettingsScreen> {
  bool _bridgeBusy = false;
  bool? _bridgeRunning;

  @override
  void initState() {
    super.initState();
    if (BridgeInstallerService.isSupported) {
      BridgeInstallerService.instance.isRunning().then((r) {
        if (mounted) setState(() => _bridgeRunning = r);
      });
    }
  }

  Future<void> _installAndStartBridge() async {
    setState(() => _bridgeBusy = true);
    final messenger = ScaffoldMessenger.of(context);
    try {
      final installer = BridgeInstallerService.instance;
      if (!installer.isInstalled() || !(await installer.isRunning())) {
        await installer.install();
      }
      final ok = await installer.startAndWait();
      if (ok) {
        messenger.showSnackBar(
          SnackBar(content: Text('Bridge 已启动 (127.0.0.1:${BridgeInstallerService.bridgePort})')),
        );
      } else {
        messenger.showSnackBar(
          const SnackBar(content: Text('Bridge 已启动但健康检查未通过，请稍后重试或查看 Bridge 日志')),
        );
      }
    } catch (e) {
      messenger.showSnackBar(SnackBar(content: Text('操作失败: $e')));
    } finally {
      final running = await BridgeInstallerService.instance.isRunning();
      if (mounted) {
        setState(() {
          _bridgeRunning = running;
          _bridgeBusy = false;
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final connectionState = ref.watch(connectionProvider);
    final storage = StorageService();
    final showBridgeSection = BridgeInstallerService.isSupported;

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
          if (showBridgeSection) ...[
            ListTile(
              leading: Icon(
                _bridgeRunning == true ? Icons.dns : Icons.dns_outlined,
                color: _bridgeRunning == true ? Colors.green : Colors.orange,
              ),
              title: const Text('本机 Bridge'),
              subtitle: Text(_bridgeRunning == true
                  ? '运行中 — http://127.0.0.1:${BridgeInstallerService.bridgePort}'
                  : _bridgeRunning == false
                      ? '未运行（Bridge 会随本应用自动启动）'
                      : '检查中...'),
              trailing: _bridgeBusy
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : IconButton(
                      icon: const Icon(Icons.play_arrow),
                      tooltip: '安装并启动 Bridge',
                      onPressed: _installAndStartBridge,
                    ),
              onTap: _bridgeBusy ? null : _installAndStartBridge,
            ),
            const Divider(),
          ],
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
