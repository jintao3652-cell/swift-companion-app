import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../chat/chat_screen.dart';
import '../status/aircraft_status_screen.dart';
import '../radar/radar_screen.dart';
import '../atc/atc_screen.dart';

class HomeScreen extends ConsumerStatefulWidget {
  const HomeScreen({Key? key}) : super(key: key);

  @override
  ConsumerState<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends ConsumerState<HomeScreen> {
  int _currentIndex = 0;

  final List<Widget> _screens = [
    const ChatScreen(),
    const RadarScreen(),
    const AtcScreen(),
    const AircraftStatusScreen(),
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: _currentIndex,
        children: _screens,
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _currentIndex,
        onDestinationSelected: (index) {
          setState(() {
            _currentIndex = index;
          });
        },
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.chat_outlined),
            selectedIcon: Icon(Icons.chat),
            label: 'Messages',
          ),
          NavigationDestination(
            icon: Icon(Icons.radar_outlined),
            selectedIcon: Icon(Icons.radar),
            label: 'Radar',
          ),
          NavigationDestination(
            icon: Icon(Icons.cell_tower_outlined),
            selectedIcon: Icon(Icons.cell_tower),
            label: 'ATC',
          ),
          NavigationDestination(
            icon: Icon(Icons.flight_outlined),
            selectedIcon: Icon(Icons.flight),
            label: 'Status',
          ),
        ],
      ),
    );
  }
}
