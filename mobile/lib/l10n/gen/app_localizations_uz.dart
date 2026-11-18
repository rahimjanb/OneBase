// ignore: unused_import
import 'package:intl/intl.dart' as intl;

import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for Uzbek (`uz`).
class AppLocalizationsUz extends AppLocalizations {
  AppLocalizationsUz([String locale = 'uz']) : super(locale);

  @override
  String get appTitle => 'OneBase';

  @override
  String get navHome => 'Bosh sahifa';

  @override
  String get navAnalytics => 'Tahlil';

  @override
  String get navTasks => 'Vazifalar';

  @override
  String get navAi => 'AI';

  @override
  String get navProfile => 'Profil';

  @override
  String get actionRetry => 'Qayta urinish';

  @override
  String get actionRefresh => 'Yangilash';

  @override
  String get actionAll => 'Hammasi';

  @override
  String get actionClose => 'Yopish';

  @override
  String get actionCancel => 'Bekor qilish';

  @override
  String get stateLoading => 'Yuklanmoqda…';

  @override
  String get stateEmpty => 'Ma\'lumot yo\'q';

  @override
  String get offlineBanner =>
      'Tarmoq yo\'q — saqlangan ma\'lumotlar ko\'rsatilmoqda';

  @override
  String get syncedJustNow => 'Hozirgina yangilandi';

  @override
  String syncedMinutes(int n) {
    return '$n daqiqa oldin yangilandi';
  }

  @override
  String syncedHours(int n) {
    return '$n soat oldin yangilandi';
  }

  @override
  String syncedAt(String date) {
    return '$date da yangilandi';
  }

  @override
  String get comingSoonTitle => 'Bo\'lim ishlab chiqilmoqda';

  @override
  String comingSoonMessage(int stage) {
    return 'Mobil ilova rejasining $stage-bosqichida paydo bo\'ladi.';
  }

  @override
  String get errorNetwork => 'Server bilan aloqa yo\'q. Internetni tekshiring.';

  @override
  String get errorTimeout =>
      'Server uzoq vaqt javob bermayapti. Yana urinib ko\'ring.';

  @override
  String get errorUnauthorized => 'Seans yopildi. Qaytadan kiring.';

  @override
  String get errorForbidden => 'Bu bo\'limga ruxsat yo\'q.';

  @override
  String get errorNotFound => 'Topilmadi.';

  @override
  String get errorRateLimited => 'Juda ko\'p urinish. Bir daqiqa kuting.';

  @override
  String get errorServer => 'Serverda xatolik. Keyinroq urinib ko\'ring.';

  @override
  String get errorInvalidResponse =>
      'Server kutilmagan javob berdi. Ilovani yangilang.';

  @override
  String get errorUnknown => 'Nimadir noto\'g\'ri ketdi.';

  @override
  String get errorNotConfigured =>
      'Ilova yig\'ishda server manzili ko\'rsatilmagan.';

  @override
  String get profileAppearance => 'Ko\'rinish';

  @override
  String get themeSystem => 'Tizimdagidek';

  @override
  String get themeLight => 'Yorug\'';

  @override
  String get themeDark => 'Qorong\'i';

  @override
  String get language => 'Til';

  @override
  String get profileAbout => 'Ilova haqida';

  @override
  String profileVersion(String version) {
    return 'Versiya $version';
  }

  @override
  String get profileServer => 'Server';

  @override
  String get unitThousand => 'ming';

  @override
  String get unitMillion => 'mln';

  @override
  String get unitBillion => 'mlrd';

  @override
  String get unitSum => 'so\'m';

  @override
  String get unitKg => 'kg';

  @override
  String get unitTon => 't';

  @override
  String get loginTitle => 'OneBase\'ga kirish';

  @override
  String get loginSubtitle => 'OneBase saytidagi hisob bilan bir xil.';

  @override
  String get loginField => 'Login yoki pochta';

  @override
  String get passwordField => 'Parol';

  @override
  String get loginButton => 'Kirish';

  @override
  String get loginInvalid => 'Login yoki parol noto\'g\'ri.';

  @override
  String get loginRequired => 'Login va parolni kiriting.';

  @override
  String get showPassword => 'Parolni ko\'rsatish';

  @override
  String get hidePassword => 'Parolni yashirish';

  @override
  String get sessionExpired =>
      'Seans yopildi: boshqa qurilmada chiqdingiz, parolni o\'zgartirdingiz yoki kirish o\'chirildi. Qaytadan kiring.';

  @override
  String get splashChecking => 'Kirish tekshirilmoqda…';

  @override
  String get restoreFailedTitle => 'Kirishni tekshirib bo\'lmadi';

  @override
  String get logout => 'Chiqish';

  @override
  String get logoutConfirmTitle => 'OneBase\'dan chiqasizmi?';

  @override
  String get logoutConfirmMessage =>
      'Bu telefondagi seans yopiladi, saqlangan ma\'lumotlar o\'chiriladi.';

  @override
  String get offlineProfile =>
      'Server bilan aloqa yo\'q — profil telefonda saqlangan.';
}
