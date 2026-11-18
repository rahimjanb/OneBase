import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Есть ли сеть (Wi-Fi, мобильная, Ethernet, VPN). «Есть сеть» не гарантирует, что сервер доступен, — это решает сам запрос;
/// флаг нужен для плашки «Нет сети» и обновления блоков, когда сеть вернулась.
final connectivityProvider = Provider<Connectivity>((ref) => Connectivity());

final isOnlineProvider = StreamProvider<bool>((ref) async* {
  final c = ref.watch(connectivityProvider);
  yield isOnline(await c.checkConnectivity());
  yield* c.onConnectivityChanged.map(isOnline).distinct();
});

bool isOnline(List<ConnectivityResult> results) => results.any((r) => r != ConnectivityResult.none);
