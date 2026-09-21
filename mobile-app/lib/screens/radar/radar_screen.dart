import 'dart:math' as math;
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../models/radar_models.dart';
import '../../providers/aircraft_state_provider.dart';
import '../../providers/radar_provider.dart';

/// 雷达页：swift 风格的相对位置雷达。
/// 本机居中、北向上（可切换跟随航向），距离环 + 飞机三角形 + ATC 菱形。
class RadarScreen extends ConsumerStatefulWidget {
  const RadarScreen({Key? key}) : super(key: key);

  @override
  ConsumerState<RadarScreen> createState() => _RadarScreenState();
}

class _RadarScreenState extends ConsumerState<RadarScreen> {
  bool _trackUp = false; // 北向上 / 航向向上
  double _rangeNm = 100; // 显示半径（海里）

  static const _ranges = [25.0, 50.0, 100.0, 200.0, 300.0];

  @override
  Widget build(BuildContext context) {
    final radar = ref.watch(radarProvider);
    final ownState = ref.watch(aircraftStateProvider);

    return Scaffold(
      backgroundColor: const Color(0xFF0B1220),
      appBar: AppBar(
        backgroundColor: const Color(0xFF0F172A),
        title: Text(
          'Radar  ${_rangeNm.toStringAsFixed(0)} nm',
          style: const TextStyle(color: Colors.white70, fontSize: 16),
        ),
        actions: [
          IconButton(
            tooltip: 'Range',
            icon: const Icon(Icons.zoom_out_map, color: Colors.white70),
            onPressed: () {
              final i = _ranges.indexOf(_rangeNm);
              _rangeNm = _ranges[(i + 1) % _ranges.length];
              setState(() {});
            },
          ),
          IconButton(
            tooltip: _trackUp ? 'Track up' : 'North up',
            icon: Icon(
              _trackUp ? Icons.navigation : Icons.explore,
              color: Colors.white70,
            ),
            onPressed: () => setState(() => _trackUp = !_trackUp),
          ),
        ],
      ),
      body: Column(
        children: [
          Expanded(
            child: LayoutBuilder(builder: (context, constraints) {
              final size = Size(constraints.maxWidth, constraints.maxHeight);
              return CustomPaint(
                size: size,
                painter: _RadarPainter(
                  own: ownState == null
                      ? null
                      : RadarAircraft(
                          callsign: ownState.callsign,
                          latitude: ownState.position.latitude,
                          longitude: ownState.position.longitude,
                          altitudeFt: ownState.position.altitude,
                          headingDeg: ownState.heading,
                          groundSpeedKt: ownState.groundSpeed,
                        ),
                  aircraft: radar.aircraft,
                  atc: radar.atc,
                  rangeNm: _rangeNm,
                  rotationDeg:
                      _trackUp ? -(ownState?.heading.toDouble() ?? 0) : 0,
                ),
                child: const SizedBox.expand(),
              );
            }),
          ),
          // 底部：本机信息条
          Container(
            color: const Color(0xFF0F172A),
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceAround,
              children: [
                _info('CALLSIGN', ownState?.callsign ?? '---'),
                _info('ALT', ownState != null
                    ? '${ownState.position.altitude} ft' : '---'),
                _info('GS', ownState != null
                    ? '${ownState.groundSpeed} kt' : '---'),
                _info('HDG', ownState != null
                    ? '${ownState.heading.toStringAsFixed(0).padLeft(3, '0')}°' : '---'),
                _info('CONTACTS', '${radar.aircraft.length}'),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _info(String label, String value) => Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(label,
              style: const TextStyle(color: Colors.white38, fontSize: 10)),
          const SizedBox(height: 2),
          Text(value,
              style: const TextStyle(
                  color: Colors.tealAccent,
                  fontSize: 14,
                  fontFamily: 'monospace')),
        ],
      );
}

class _RadarPainter extends CustomPainter {
  final RadarAircraft? own;
  final List<RadarAircraft> aircraft;
  final List<AtcStation> atc;
  final double rangeNm;
  final double rotationDeg;

  _RadarPainter({
    required this.own,
    required this.aircraft,
    required this.atc,
    required this.rangeNm,
    required this.rotationDeg,
  });

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2 - 20);
    final radius = math.min(size.width, size.height) / 2 - 40;

    _drawGrid(canvas, center, radius);
    if (own != null) _drawContacts(canvas, center, radius);
    _drawOwnShip(canvas, center);
  }

  void _drawGrid(Canvas canvas, Offset center, double radius) {
    // 背景圈
    final bgPaint = Paint()..color = const Color(0xFF0E1A2E);
    canvas.drawCircle(center, radius + 24, bgPaint);

    final ringPaint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1
      ..color = Colors.teal.withOpacity(0.35);

    // 距离环（25% 50% 75% 100%）
    for (final f in const [0.25, 0.5, 0.75, 1.0]) {
      canvas.drawCircle(center, radius * f, ringPaint);
      final label = (rangeNm * f).toStringAsFixed(0);
      _drawText(canvas, label,
          center + Offset(radius * f * 0.7071, -radius * f * 0.7071) + const Offset(4, -6),
          Colors.teal.withOpacity(0.6), 9);
    }

    // 十字线
    canvas.drawLine(center - Offset(radius, 0), center + Offset(radius, 0), ringPaint);
    canvas.drawLine(center - Offset(0, radius), center + Offset(0, radius), ringPaint);

    // 方位刻度 N/E/S/W
    _drawText(canvas, 'N', center - Offset(0, radius + 12), Colors.white54, 12);
    _drawText(canvas, 'S', center + Offset(0, radius + 4), Colors.white54, 12);
    _drawText(canvas, 'E', center + Offset(radius + 6, 0), Colors.white54, 12);
    _drawText(canvas, 'W', center - Offset(radius + 14, 0), Colors.white54, 12);
  }

  void _drawContacts(Canvas canvas, Offset center, double radius) {
    final rotation = rotationDeg * math.pi / 180;
    final ownLat = _deg2rad(own!.latitude);
    final ownLon = _deg2rad(own!.longitude);

    void project(double lat, double lon, void Function(Offset, double bearingDeg) draw)
    {
      final dLat = _deg2rad(lat) - ownLat;
      final dLon = _deg2rad(lon) - ownLon;
      // 等距圆柱近似（短距离足够）
      final meanLat = (ownLat + _deg2rad(lat)) / 2;
      final dEast = dLon * math.cos(meanLat);
      final dNorth = dLat;
      final distRad = math.sqrt(dEast * dEast + dNorth * dNorth);
      final distNm = distRad * 3440.065;
      if (distNm > rangeNm) return;
      var bearing = math.atan2(dEast, dNorth); // 北为 0
      // 旋转（Track up）
      bearing += rotation;
      final r = (distNm / rangeNm) * radius;
      final pos = center + Offset(math.sin(bearing) * r, -math.cos(bearing) * r);
      draw(pos, distNm);
    }

    // 飞机：三角形（指向航向）
    for (final a in aircraft) {
      if (a.callsign == own?.callsign) continue;
      project(a.latitude, a.longitude, (pos, distNm) {
        final headingRad = (a.headingDeg + rotationDeg) * math.pi / 180;
        _drawTriangle(canvas, pos, 7, headingRad, Colors.amberAccent);
        _drawText(canvas,
            '${a.callsign}\n${(a.altitudeFt / 100).round()} ${a.groundSpeedKt}',
            pos + const Offset(10, -12), Colors.white70, 9);
      });
    }

    // ATC：菱形 + 频率
    for (final s in atc) {
      // ATC 没有单独的坐标推送（Bridge 把 ATC 也放进 nearby 列表），按 callsign 查找
      final m = aircraft.where((a) => a.callsign == s.callsign).toList();
      if (m.isEmpty) continue;
      project(m.first.latitude, m.first.longitude, (pos, distNm) {
        _drawDiamond(canvas, pos, 6, Colors.lightGreenAccent);
        _drawText(canvas, '${s.callsign}\n${s.frequencyText}',
            pos + const Offset(10, -12), Colors.lightGreenAccent, 9);
      });
    }
  }

  void _drawOwnShip(Canvas canvas, Offset center) {
    final headingRad = ((own?.headingDeg ?? 0) + rotationDeg) * math.pi / 180;
    _drawTriangle(canvas, center, 11, headingRad, Colors.cyanAccent, filled: true);
    // 本机光圈
    canvas.drawCircle(center, 16,
        Paint()..color = Colors.cyanAccent.withOpacity(0.12));
  }

  void _drawTriangle(Canvas canvas, Offset c, double r, double rotationRad,
      Color color, {bool filled = false}) {
    final path = Path()
      ..moveTo(c.dy * 0 + c.dx, c.dy - r); // 顶点向上
    path.reset();
    path.moveTo(0, -r);
    path.lineTo(r * 0.866, r * 0.5);
    path.lineTo(-r * 0.866, r * 0.5);
    path.close();
    final paint = Paint()
      ..color = color
      ..style = filled ? PaintingStyle.fill : PaintingStyle.stroke
      ..strokeWidth = 1.6;
    canvas.save();
    canvas.translate(c.dx, c.dy);
    canvas.rotate(rotationRad);
    canvas.drawPath(path, paint);
    canvas.restore();
  }

  void _drawDiamond(Canvas canvas, Offset c, double r, Color color) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.fill;
    final path = Path()
      ..moveTo(c.dx, c.dy - r)
      ..lineTo(c.dx + r, c.dy)
      ..lineTo(c.dx, c.dy + r)
      ..lineTo(c.dx - r, c.dy)
      ..close();
    canvas.drawPath(path, paint);
  }

  void _drawText(Canvas canvas, String text, Offset pos, Color color, double size,
      {bool monospace = true}) {
    final tp = TextPainter(
      text: TextSpan(
        text: text,
        style: TextStyle(
          color: color,
          fontSize: size,
          fontFamily: monospace ? 'monospace' : null,
          height: 1.15,
        ),
      ),
      textDirection: TextDirection.ltr,
    )..layout();
    tp.paint(canvas, pos);
  }

  double _deg2rad(double d) => d * math.pi / 180;

  @override
  bool shouldRepaint(covariant _RadarPainter old) =>
      old.own != own ||
      old.aircraft != aircraft ||
      old.atc != atc ||
      old.rangeNm != rangeNm ||
      old.rotationDeg != rotationDeg;
}
