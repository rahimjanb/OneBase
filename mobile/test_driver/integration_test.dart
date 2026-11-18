import 'dart:io';

import 'package:integration_test/integration_test_driver_extended.dart';

/// Драйвер `flutter drive`: снимки экрана из integration-тестов сохраняются в папку SHOTS_DIR (по умолчанию build/shots).
Future<void> main() => integrationDriver(
      onScreenshot: (name, bytes, [args]) async {
        final dir = Directory(Platform.environment['SHOTS_DIR'] ?? 'build/shots')..createSync(recursive: true);
        File('${dir.path}/$name.png').writeAsBytesSync(bytes);
        return true;
      },
    );
