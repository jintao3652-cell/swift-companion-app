import 'package:shared_preferences/shared_preferences.dart';

class StorageService {
  static const String _keyToken = 'auth_token';
  static const String _keyUserId = 'user_id';
  static const String _keyBridgeAddress = 'bridge_address';
  static const String _keyDeviceId = 'device_id';
  static const String _keyIsPaired = 'is_paired';

  Future<void> saveAuthToken(String token) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_keyToken, token);
  }

  Future<String?> getAuthToken() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_keyToken);
  }

  Future<void> saveUserId(String userId) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_keyUserId, userId);
  }

  Future<String?> getUserId() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_keyUserId);
  }

  Future<void> saveBridgeAddress(String address) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_keyBridgeAddress, address);
  }

  Future<String?> getBridgeAddress() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_keyBridgeAddress);
  }

  Future<void> saveDeviceId(String deviceId) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_keyDeviceId, deviceId);
  }

  Future<String?> getDeviceId() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_keyDeviceId);
  }

  Future<void> setIsPaired(bool isPaired) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setInt(_keyIsPaired, isPaired ? 1 : 0);
  }

  Future<bool> getIsPaired() async {
    final prefs = await SharedPreferences.getInstance();
    return (prefs.getInt(_keyIsPaired) ?? 0) == 1;
  }

  Future<void> clearAll() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.clear();
  }

  // Generate unique device ID
  Future<String> getOrCreateDeviceId() async {
    String? deviceId = await getDeviceId();

    if (deviceId == null) {
      deviceId = 'device_${DateTime.now().millisecondsSinceEpoch}';
      await saveDeviceId(deviceId);
    }

    return deviceId;
  }
}
