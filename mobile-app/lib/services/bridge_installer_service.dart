import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart';

/// 本机 Bridge 安装器 / 启动器（仅 Windows 桌面端生效）。
///
/// 工作方式：
/// - 桌面版发布包内自带 `bridge/` 子目录（self-contained Bridge，含运行时）
/// - 首次启动时把 `bridge/` 复制到 %LOCALAPPDATA%\<appDir>\bridge（即"安装"）
/// - Companion 启动时自动拉起 Bridge 进程并等待健康检查通过
/// - 设置页提供"安装并启动 Bridge"手动按钮
class BridgeInstallerService {
  BridgeInstallerService._();
  static final BridgeInstallerService instance = BridgeInstallerService._();

  // ==== 每个应用只需改这三个常量 ====
  static const String bridgeExeName = 'SwiftBridge.exe';
  static const String appDataDirName = 'SwiftCompanion';
  static const int bridgePort = 5000;

  static bool get isSupported => !kIsWeb && Platform.isWindows;

  Process? _process;

  /// Bridge 安装目录: %LOCALAPPDATA%\<appDataDirName>\bridge
  String get installDir {
    final appData = Platform.environment['LOCALAPPDATA'] ??
        Platform.environment['APPDATA'] ??
        Directory.systemTemp.path;
    return '$appData\\$appDataDirName\\bridge';
  }

  String get installedExePath => '$installDir\\$bridgeExeName';

  /// 发布包内自带的 bridge 目录（exe 同级的 bridge/）
  String? get bundledBridgeDir {
    try {
      final exeDir = File(Platform.resolvedExecutable).parent.path;
      final bundled = '$exeDir\\bridge';
      if (File('$bundled\\$bridgeExeName').existsSync()) return bundled;
    } catch (e) {
      debugPrint('findBundledBridge failed: $e');
    }
    return null;
  }

  bool isInstalled() => File(installedExePath).existsSync();

  /// 安装：把发布包内的 bridge/ 复制到 %LOCALAPPDATA%
  Future<void> install() async {
    final src = bundledBridgeDir;
    if (src == null) {
      throw Exception('未找到自带的 bridge 目录（请使用完整发布包）');
    }
    final dst = Directory(installDir);
    if (dst.existsSync()) {
      // 若 Bridge 正在运行先停掉，避免文件占用
      try {
        _process?.kill();
      } catch (_) {}
      await Future.delayed(const Duration(milliseconds: 500));
      dst.deleteSync(recursive: true);
    }
    await _copyDirectory(Directory(src), dst);
    debugPrint('Bridge installed to $installDir');
  }

  /// 本机 Bridge 是否在运行（健康检查）
  Future<bool> isRunning({Duration timeout = const Duration(seconds: 2)}) async {
    try {
      final client = HttpClient();
      client.connectionTimeout = timeout;
      final req = await client
          .getUrl(Uri.parse('http://127.0.0.1:$bridgePort/api/status/health'))
          .timeout(timeout);
      final resp = await req.close().timeout(timeout);
      final ok = resp.statusCode == 200;
      client.close(force: true);
      return ok;
    } catch (_) {
      return false;
    }
  }

  /// 启动 Bridge 进程（detached，Companion 退出后 Bridge 继续运行）
  Future<void> start() async {
    if (!isInstalled()) {
      throw Exception('Bridge 尚未安装');
    }
    if (await isRunning()) {
      debugPrint('Bridge already running on port $bridgePort');
      return;
    }
    _process = await Process.start(
      installedExePath,
      const [],
      workingDirectory: installDir,
      mode: ProcessStartMode.detached,
    );
    debugPrint('Bridge process started (pid ${_process?.pid})');
  }

  /// 启动并等待健康检查通过（最多 [waitTimeout]）
  Future<bool> startAndWait({
    Duration waitTimeout = const Duration(seconds: 20),
  }) async {
    await start();
    final deadline = DateTime.now().add(waitTimeout);
    while (DateTime.now().isBefore(deadline)) {
      if (await isRunning()) return true;
      await Future.delayed(const Duration(milliseconds: 500));
    }
    return false;
  }

  /// Companion 启动时调用：Windows 下确保 Bridge 已安装并自动启动。
  /// 返回 'running'（已启动）/ 'bundled'（有安装包但启动失败）/ null（不适用）。
  Future<String?> autoStartIfNeeded() async {
    if (!isSupported) return null;
    try {
      // 已在运行（可能是上次启动残留）→ 无需操作
      if (await isRunning()) return 'running';

      // 未安装则先安装（需要发布包内自带 bridge/）
      if (!isInstalled()) {
        if (bundledBridgeDir == null) return null; // 纯绿色运行，无 bridge 可装
        await install();
      }
      final ok = await startAndWait();
      return ok ? 'running' : 'bundled';
    } catch (e) {
      debugPrint('Bridge autoStart failed: $e');
      return 'bundled';
    }
  }

  Future<void> _copyDirectory(Directory src, Directory dst) async {
    await dst.create(recursive: true);
    await for (final entity in src.list()) {
      final newPath = '${dst.path}\\${entity.path.split('\\').last}';
      if (entity is Directory) {
        await _copyDirectory(entity, Directory(newPath));
      } else if (entity is File) {
        await entity.copy(newPath);
      }
    }
  }
}
