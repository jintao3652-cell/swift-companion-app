import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'screens/pairing/pairing_screen.dart';
import 'screens/home/home_screen.dart';
import 'config/theme.dart';
import 'services/storage_service.dart';
import 'services/push_notification_service.dart';
import 'providers/connection_provider.dart';
import 'providers/message_provider.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // 初始化 Hive
  await Hive.initFlutter();

  runApp(
    const ProviderScope(
      child: VatsimCompanionApp(),
    ),
  );
}

class VatsimCompanionApp extends StatelessWidget {
  const VatsimCompanionApp({Key? key}) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'VATSIM Companion',
      theme: AppTheme.lightTheme,
      darkTheme: AppTheme.darkTheme,
      themeMode: ThemeMode.system,
      home: const SplashScreen(),
      routes: {
        '/pairing': (context) => const PairingScreen(),
        '/home': (context) => const HomeScreen(),
      },
    );
  }
}

class SplashScreen extends ConsumerStatefulWidget {
  const SplashScreen({Key? key}) : super(key: key);

  @override
  ConsumerState<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends ConsumerState<SplashScreen> {
  final StorageService _storageService = StorageService();
  final PushNotificationService _pushService = PushNotificationService();

  @override
  void initState() {
    super.initState();
    _initialize();
  }

  Future<void> _initialize() async {
    try {
      // 初始化推送通知服务
      await _pushService.initialize();

      // Test notification after 2 seconds
      Future.delayed(const Duration(seconds: 2), () {
        _pushService.showTestNotification();
      });

      // 短暂延迟以显示启动画面
      await Future.delayed(const Duration(seconds: 1));

      // 检查是否已配对
      final isPaired = await _storageService.getIsPaired();

      if (isPaired) {
        // 获取保存的配对信息
        final token = await _storageService.getAuthToken();
        final bridgeAddress = await _storageService.getBridgeAddress();

        if (token != null && bridgeAddress != null) {
          // 尝试重新连接
          try {
            final connectionNotifier = ref.read(connectionProvider.notifier);
            await connectionNotifier.connect(bridgeAddress, token);

            // 连接成功后立即初始化消息 Provider，确保 onMessageReceived 监听已注册
            ref.read(messagesProvider.notifier);

            // 注册推送 Token
            if (_pushService.fcmToken != null) {
              final deviceId = await _storageService.getOrCreateDeviceId();
              await connectionNotifier.wsService.registerPushToken(
                _pushService.fcmToken!,
                deviceId,
              );
            }

            // 跳转到主页
            if (mounted) {
              Navigator.pushReplacementNamed(context, '/home');
              return;
            }
          } catch (e) {
            // 连接失败，清除配对信息并跳转到配对页面
            print('Reconnection failed: $e');
            await _storageService.clearAll();
          }
        }
      }

      // 未配对或重新连接失败，跳转到配对页面
      if (mounted) {
        Navigator.pushReplacementNamed(context, '/pairing');
      }
    } catch (e) {
      print('Initialization error: $e');
      // 出错时也跳转到配对页面
      if (mounted) {
        Navigator.pushReplacementNamed(context, '/pairing');
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(
              Icons.flight_takeoff,
              size: 80,
              color: Theme.of(context).primaryColor,
            ),
            const SizedBox(height: 24),
            Text(
              'VATSIM Companion',
              style: Theme.of(context).textTheme.headlineMedium,
            ),
            const SizedBox(height: 16),
            const CircularProgressIndicator(),
            const SizedBox(height: 16),
            Text(
              'Initializing...',
              style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                    color: Colors.grey,
                  ),
            ),
          ],
        ),
      ),
    );
  }
}
