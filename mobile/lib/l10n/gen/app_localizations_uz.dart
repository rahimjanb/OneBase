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

  @override
  String get actionDetails => 'Batafsil';

  @override
  String get actionOpen => 'Ochish';

  @override
  String periodRange(String from, String to) {
    return '$from — $to';
  }

  @override
  String get greetingMorning => 'Xayrli tong';

  @override
  String get greetingDay => 'Xayrli kun';

  @override
  String get greetingEvening => 'Xayrli kech';

  @override
  String get greetingNight => 'Xayrli tun';

  @override
  String greetingName(String greeting, String name) {
    return '$greeting, $name';
  }

  @override
  String get periodTitle => 'Davr';

  @override
  String get periodLatest => 'So\'nggi ma\'lumotlar';

  @override
  String get periodLatestHint => 'Savdoning so\'nggi oyi; moliya — oy boshidan';

  @override
  String get organizationTitle => '1C tashkiloti';

  @override
  String get organizationAll => 'Barcha tashkilotlar';

  @override
  String get dashboardNothing =>
      'Rolingiz uchun bosh sahifada hozircha faqat voqealar. Bo\'limlarni administrator ochadi.';

  @override
  String get salesTitle => 'Savdo';

  @override
  String salesTitleMonth(String month) {
    return 'Savdo · $month';
  }

  @override
  String get kpiRevenue => 'Tushum';

  @override
  String get kpiSold => 'Sotildi';

  @override
  String get kpiClients => 'Mijozlar (AKB)';

  @override
  String get kpiPlan => 'Oylik reja';

  @override
  String get kpiNoPlan => 'Reja belgilanmagan';

  @override
  String kpiThrough(String date) {
    return '$date gacha ma\'lumot';
  }

  @override
  String kpiForecast(String value) {
    return 'prognoz $value';
  }

  @override
  String kpiPerAgent(String value, String target) {
    return 'agentga $value · maqsad $target';
  }

  @override
  String get vsSameDays => 'o\'tgan oyning shu kunlariga';

  @override
  String get vsSameDaysTitle => 'O\'tgan oyning shu kunlariga nisbatan';

  @override
  String get salesTrendTitle => 'Savdo dinamikasi';

  @override
  String salesTrendCaption(String month, String total) {
    return '$month: kunlar bo\'yicha $total';
  }

  @override
  String salesTrendSemantics(String month, String total) {
    return '$month uchun kunlik savdo grafigi, jami $total';
  }

  @override
  String get salesTrendToday =>
      'Och rangli ustun — bugungi to\'liq bo\'lmagan kun.';

  @override
  String get salesTrendEmpty => 'Davr uchun savdo yo\'q.';

  @override
  String get detailVolume => 'Hajm va reja';

  @override
  String get detailMoney => 'Pul';

  @override
  String get detailClients => 'Mijozlar va tashriflar';

  @override
  String get detailByDay => 'Kunlar bo\'yicha';

  @override
  String get rowPlan => 'Reja';

  @override
  String get rowForecast => 'Oy oxiriga prognoz';

  @override
  String rowForecastPlan(String value) {
    return 'rejaning $value';
  }

  @override
  String get rowPace => 'Kunlik sur\'at';

  @override
  String get rowRequired => 'Rejaga kuniga kerak';

  @override
  String get rowRevenuePerOutlet => 'Bir nuqtaga tushum';

  @override
  String get rowAgents => 'Faol agentlar';

  @override
  String get rowAkbPerAgent => 'Agentga mijozlar';

  @override
  String get rowVisits => 'Tashriflar';

  @override
  String get rowVisitsNoOrder => 'Buyurtmasiz tashriflar';

  @override
  String get rowConversion => 'Tashriflar konversiyasi';

  @override
  String get rowFlags => 'Agentlar bo\'yicha signallar';

  @override
  String rowFlagsHint(int critical, int risk) {
    return '$critical ta jiddiy · $risk ta xavfli';
  }

  @override
  String rowTarget(String value) {
    return 'maqsad $value';
  }

  @override
  String rowOfTarget(String value) {
    return 'maqsadning $value';
  }

  @override
  String get financeTitle => 'Moliya · 1C';

  @override
  String get financeNoData => '1C ma\'lumotlari hali yo\'q';

  @override
  String get financeNetRevenue => 'Sof tushum';

  @override
  String get financeExpenses => 'Xarajatlar';

  @override
  String get financeNetProfit => 'Sof foyda';

  @override
  String get financeTrendTitle => '12 oy: tushum va xarajatlar';

  @override
  String get financePnl => 'Foyda va zararlar';

  @override
  String financePrevious(String value) {
    return 'o\'tgan davr: $value';
  }

  @override
  String get financeBalances => 'Pul va qarzlar';

  @override
  String get financeCash => 'Hisob va kassadagi pul';

  @override
  String get financeReceivables => 'Debitorlik qarzi';

  @override
  String get financePayables => 'Kreditorlik qarzi';

  @override
  String get financeByMonth => 'Oylar bo\'yicha';

  @override
  String get deptTitle => 'Bo\'linmalar';

  @override
  String get hrAttendance => 'Bugungi kelish';

  @override
  String hrPresentOf(String present, String scheduled) {
    return 'jadval bo\'yicha $scheduled dan $present';
  }

  @override
  String get hrLate => 'Kechikdi';

  @override
  String get hrAbsent => 'Kelmadi';

  @override
  String get hrOnLeave => 'Ta\'tilda';

  @override
  String hrCompare(String value) {
    return 'O\'tgan ish kuni shu vaqtda — $value';
  }

  @override
  String get hrNoSchedule => 'Bugun jadval bo\'yicha hech kim yo\'q.';

  @override
  String get hrDivisionsAll => 'Barcha bo\'linmalar';

  @override
  String hrSynced(String when) {
    return 'Verifix: $when';
  }

  @override
  String get tasksOpen => 'Ochiq';

  @override
  String get tasksInProgress => 'Jarayonda';

  @override
  String get tasksReview => 'Tekshiruvda';

  @override
  String get tasksOverdue => 'Muddati o\'tgan';

  @override
  String get tasksMine => 'Mening yaqin vazifalarim';

  @override
  String get tasksNoUpcoming => 'Yaqin vazifalar yo\'q.';

  @override
  String tasksDue(String date) {
    return '$date gacha';
  }

  @override
  String get tasksDepartments => 'Bo\'limlar';

  @override
  String tasksDeptLine(int open, int overdue) {
    return '$open ochiq · $overdue muddati o\'tgan';
  }

  @override
  String get aiTitle => 'Kun fokusi · AI';

  @override
  String get aiCritical => 'Jiddiy';

  @override
  String get aiProblems => 'Muammolar';

  @override
  String get aiOpportunities => 'Imkoniyatlar';

  @override
  String get aiNone => 'AI muammo topmadi.';

  @override
  String aiChecked(String when) {
    return '$when tekshirildi';
  }

  @override
  String get aiWhatToDo => 'Nima qilish kerak';

  @override
  String get aiAll => 'AI\'ning barcha topilmalari';

  @override
  String get severityCritical => 'Jiddiy';

  @override
  String get severityWarning => 'Diqqat';

  @override
  String get severityOpportunity => 'Imkoniyat';

  @override
  String get eventsTitle => 'So\'nggi voqealar';

  @override
  String get eventsEmpty => 'Hozircha voqealar yo\'q.';

  @override
  String eventsUnread(int n) {
    return '$n ta o\'qilmagan';
  }

  @override
  String get agoJustNow => 'hozirgina';

  @override
  String agoMinutes(int n) {
    return '$n daqiqa oldin';
  }

  @override
  String agoHours(int n) {
    return '$n soat oldin';
  }

  @override
  String agoYesterday(String time) {
    return 'kecha, $time';
  }

  @override
  String get rowExecution => 'Reja bajarilishi';
}
