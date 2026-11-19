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
  String get navAi => 'Konsultant';

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
  String get deptTitle => 'Bo\'limlar';

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

  @override
  String get moduleDepartments => 'Bo\'limlar';

  @override
  String get moduleSales => 'Mijozlar';

  @override
  String get moduleFinance => 'Moliya';

  @override
  String get moduleProduction => 'Ishlab chiqarish';

  @override
  String get moduleWarehouse => 'Ombor';

  @override
  String get moduleRequests => 'Arizalar';

  @override
  String get moduleHr => 'HR';

  @override
  String get moduleInsights => 'AI hisobot';

  @override
  String get moduleNotifications => 'Bildirishnomalar';

  @override
  String get moduleSettings => 'Sozlamalar';

  @override
  String get navOpenMenu => 'Bo\'limlar menyusini ochish';

  @override
  String get navCloseMenu => 'Menyuni yopish';

  @override
  String get navSections => 'Asosiy bo\'limlar';

  @override
  String get searchTitle => 'Qidiruv';

  @override
  String notificationsUnread(int n) {
    return 'Bildirishnomalar: $n ta o\'qilmagan';
  }

  @override
  String get homePlanDone => 'Reja bajarildi';

  @override
  String get homeAttendanceToday => 'Bugungi davomat';

  @override
  String get homeAllDeptsOk => 'barcha bo\'limlar me\'yorda';

  @override
  String homeDeptsAttention(int n) {
    return '$n ta bo\'lim — e\'tibor';
  }

  @override
  String get homeWaiting => 'Sizni kutmoqda';

  @override
  String homeAwaitingApproval(int n) {
    return 'Tasdiqlashingizni kutmoqda: $n';
  }

  @override
  String homePickUp(int n) {
    return 'Omborda olish mumkin: $n';
  }

  @override
  String get homePickUpDept =>
      'Bo\'lim arizalari bo\'yicha tovar omborda kutmoqda — omborchidan oling.';

  @override
  String get homePickUpMine =>
      'Arizalaringiz bo\'yicha tovar omborda kutmoqda — omborchidan oling.';

  @override
  String homeAcceptedWeek(String sum) {
    return 'Hafta davomida omborga qabul qilindi: $sum';
  }

  @override
  String get filterPeriod7d => '7 kun';

  @override
  String get filterPeriod30d => '30 kun';

  @override
  String get filterPeriodQuarter => 'Chorak';

  @override
  String get filterPeriodYear => 'Yil';

  @override
  String get filterAllDepartments => 'Barcha bo\'limlar';

  @override
  String filterDepartmentsN(int n) {
    return 'Bo\'limlar: $n';
  }

  @override
  String get filterApply => 'Qo\'llash';

  @override
  String get filterReset => 'Tozalash';

  @override
  String get heroSalesPlan => 'Savdo rejasining bajarilishi';

  @override
  String get heroVsSameDays => 'o\'tgan oyning shu kunlariga';

  @override
  String get heroVsPrevDay => 'oldingi ish kuniga';

  @override
  String get heroGoal => 'maqsad';

  @override
  String get heroNorm => 'me\'yor';

  @override
  String get unitPp => 'f.p.';

  @override
  String get statForecast => 'Prognoz';

  @override
  String get statToMonthEnd => 'oy oxiriga';

  @override
  String statOverdueN(int n) {
    return '$n ta muddati o\'tgan';
  }

  @override
  String statDueWeekN(int n) {
    return '$n ta shu hafta muddati';
  }

  @override
  String get statAttendanceMonth => 'Oylik davomat';

  @override
  String get statPerMonth => 'oy uchun';

  @override
  String get quickTask => 'Vazifa';

  @override
  String get quickAskAi => 'AI dan so\'rash';

  @override
  String get quickExport => 'Eksport';

  @override
  String get quickReportAi => 'AI hisobot';

  @override
  String get focusTitle => 'Kun diqqati';

  @override
  String focusDepartments(int n) {
    return '$n ta bo\'lim';
  }

  @override
  String focusActiveTasks(int n) {
    return '$n ta faol vazifa';
  }

  @override
  String get focusFallback => 'AI-konsultant topilmalari';

  @override
  String get focusCalm => 'Bugun hammasi tinch';

  @override
  String focusMore(int n) {
    return 'Yana $n';
  }

  @override
  String focusAskQuestion(String list) {
    return '«Kun diqqati» topilmalarini tahlil qilib, bugun nima qilish kerakligini ayting: $list';
  }

  @override
  String get focusAskEmpty => 'Bugun kompaniyada nimani tekshirish kerak?';

  @override
  String homeAskQuestion(String period, String scope) {
    return 'Kompaniya ko\'rsatkichlarini «$period» davri uchun tahlil qiling$scope: hozir nimaga e\'tibor berish va birinchi navbatda nima qilish kerak?';
  }

  @override
  String get catFinance => 'Moliya';

  @override
  String get catSales => 'Savdo';

  @override
  String get catMarketing => 'Marketing';

  @override
  String get catHr => 'HR';

  @override
  String get catProduction => 'Ishlab chiqarish';

  @override
  String get catSupply => 'Ta\'minot';

  @override
  String get dynTitle => 'Dinamika';

  @override
  String get dynMore => 'Batafsil';

  @override
  String get dynSales => 'Savdo';

  @override
  String get dynRevenue => 'Tushum';

  @override
  String get dynAttendance => 'Davomat';

  @override
  String get dynAverage => 'O\'rtacha';

  @override
  String get dynBest => 'Eng yaxshi hafta';

  @override
  String get dynAbove => 'Maqsaddan yuqori';

  @override
  String dynOfTotal(int n, int total) {
    return '$total dan $n';
  }

  @override
  String dynForWeeks(int n) {
    return '$n haftada';
  }

  @override
  String dynGoal(String n) {
    return 'maqsad $n%';
  }

  @override
  String dynWeekValue(int w, String value) {
    return '$w-hafta · $value';
  }

  @override
  String dynCaption(int w, String days) {
    return '$w-hafta — joriy, $days ma\'lumoti';
  }

  @override
  String daysN(int n) {
    return '$n kun';
  }

  @override
  String get dynEmpty => 'Davr uchun ma\'lumot yo\'q';

  @override
  String dynSemantics(String name, String avg, String best) {
    return '$name haftalar bo\'yicha: o\'rtacha $avg, eng yaxshi hafta $best';
  }

  @override
  String tasksAllN(String n) {
    return 'Hammasi $n';
  }

  @override
  String get tasksNone => 'Davr uchun vazifalar yo\'q';

  @override
  String get tasksDonePct => 'bajarildi';

  @override
  String get tasksDone => 'Bajarildi';

  @override
  String get tasksInWork => 'Jarayonda';

  @override
  String get tasksOnReview => 'Tekshiruvda';

  @override
  String get tasksOverdueLabel => 'Muddati o\'tgan';

  @override
  String tasksOverdueBanner(int n) {
    return '$n ta vazifa muddati o\'tgan';
  }

  @override
  String tasksOverdueIn(int n, String name) {
    return ' — «$name» bo\'limida $n ta';
  }

  @override
  String deptOkN(int n) {
    return '$n me\'yorda';
  }

  @override
  String deptAttentionN(int n) {
    return '$n e\'tibor';
  }

  @override
  String get deptNone => 'Bo\'limlar bo\'yicha ma\'lumot yo\'q';

  @override
  String get deptSalesMetric => 'Oylik reja bajarilishi';

  @override
  String get deptHrMetric => 'Oy boshidan davomat';

  @override
  String deptWeekTrend(String n) {
    return '$n hafta';
  }

  @override
  String get statusAttention => 'E\'tibor';

  @override
  String get statusOk => 'Me\'yorda';

  @override
  String get reportTitle => 'Haftalik AI hisobot';

  @override
  String reportLine(String risks, String recs) {
    return 'bo\'limlar bo\'yicha $risks va $recs';
  }

  @override
  String risksN(int n) {
    return '$n ta xavf';
  }

  @override
  String recsN(int n) {
    return '$n ta tavsiya';
  }

  @override
  String get dataUpdatedNow => 'Ma\'lumotlar hozirgina yangilandi';

  @override
  String dataUpdatedMin(int n) {
    return 'Ma\'lumotlar $n daqiqa oldin yangilandi';
  }

  @override
  String get dataPullHint => 'yangilash uchun torting';

  @override
  String get dataFailed => 'Yangilab bo\'lmadi';

  @override
  String get dataUpdating => 'Yangilanmoqda…';

  @override
  String offlineSince(String time) {
    return 'Aloqa yo\'q · $time holatidagi ma\'lumotlar';
  }

  @override
  String get exportTitle => 'Sharhni eksport qilish';

  @override
  String get exportCsv => 'Excel (CSV)';

  @override
  String get exportCsvHint =>
      'Ko\'rsatkichlar, vazifalar va bo\'limlar jadvalda';

  @override
  String get homeMoreSales => 'Oy savdosi';

  @override
  String get homeMoreFinance => '1C moliya';

  @override
  String get homeKeyMetric => 'Asosiy ko\'rsatkich';

  @override
  String homeAgentsN(int n) {
    return '$n ta savdo vakili';
  }

  @override
  String get homeSearchHint => 'Bo\'limlar, xodimlar, mijozlar';

  @override
  String get profileOpen => 'Profilni ochish';

  @override
  String get myDayTitle => 'Mening kunim';

  @override
  String myDayShift(String from, String to) {
    return 'Smena $from–$to';
  }

  @override
  String get myDayArrival => 'Keldingiz';

  @override
  String get myDayDeparture => 'Ketdingiz';

  @override
  String get myDayWorked => 'Ishlangan';

  @override
  String get myDayLateWeek => 'Haftadagi kechikishlar';

  @override
  String myDayHoursMinutes(int h, int m) {
    return '$h soat $m daq';
  }

  @override
  String get dayStatus_not_started => 'Ish kuni boshlanmagan';

  @override
  String get dayStatus_at_work => 'Ishda';

  @override
  String get dayStatus_on_break => 'Tanaffusda';

  @override
  String get dayStatus_out => 'Chiqib ketdingiz';

  @override
  String get dayStatus_finished => 'Ish kuni tugadi';

  @override
  String get dayStatus_day_off => 'Dam olish kuni';

  @override
  String get dayStatus_on_leave => 'Hujjat bo\'yicha yo\'qlik';

  @override
  String get dayStatus_absent => 'Kelmagan';

  @override
  String get dayStatus_other => 'Ma\'lumot yo\'q';

  @override
  String dayLate(int n) {
    return '$n daqiqa kechikish';
  }

  @override
  String get myTasksTitle => 'Mening vazifalarim';

  @override
  String get myTasksNew => 'Yangi';

  @override
  String get myTasksWaiting => 'Kutmoqda';

  @override
  String get prefsTitle => 'Nimalarni yuborish';

  @override
  String get prefsHint =>
      'O\'chirilgan bo\'limlar barcha qurilmalarda bildirishnomalar va hisoblagichlardan yo\'qoladi.';

  @override
  String get prefCat_tasks => 'Vazifalar';

  @override
  String get prefCat_ai => 'AI';

  @override
  String get prefCat_hr => 'HR';

  @override
  String get prefCat_sales => 'Savdo';

  @override
  String get prefCat_system => 'Tizim';

  @override
  String get prefsQueued => 'Tarmoq paydo bo\'lganda saqlanadi';

  @override
  String prefsRejected(String error) {
    return 'Saqlab bo\'lmadi: $error';
  }

  @override
  String get telegramTitle => 'Telegram';

  @override
  String get telegramNone =>
      'Telegram bog\'lanmagan. AI-agent sizga Telegramda sizning huquqlaringiz bilan javob berishi uchun akkauntni bog\'lang.';

  @override
  String get telegramFull => 'to\'liq kirish';

  @override
  String get telegramLimited => 'faqat yuborilgan ma\'lumotlar';

  @override
  String get telegramLink => 'Telegramni bog\'lash';

  @override
  String get telegramUnlink => 'Uzish';

  @override
  String get telegramUnlinkConfirm =>
      'Agent qayta bog\'lamaguningizcha sizga huquqlaringiz bilan javob bermaydi.';

  @override
  String get telegramCodeTitle => 'Bog\'lash kodi';

  @override
  String telegramCodeHint(String to) {
    return 'Ushbu kodni Telegramga yuboring: $to. Kod 15 daqiqa amal qiladi.';
  }

  @override
  String get actionCopy => 'Nusxalash';

  @override
  String get copied => 'Nusxalandi';

  @override
  String get securityTitle => 'Xavfsizlik';

  @override
  String get securityPassword => 'Parol';

  @override
  String get securityPasswordHint =>
      'Parolni OneBase administratori o\'zgartiradi';

  @override
  String get securitySession => 'Seans';

  @override
  String get securitySessionHint =>
      'Chiqish faqat shu telefondagi seansni yopadi';

  @override
  String get securityStorage => 'Telefondagi ma\'lumotlar';

  @override
  String securityStorageHint(int n) {
    return 'Token himoyalangan xotirada, hisobotlar ilova keshida ($n ta yozuv)';
  }

  @override
  String get securityClearCache => 'Keshni tozalash';

  @override
  String get cacheCleared => 'Kesh tozalandi';

  @override
  String get syncTitle => 'Sinxronlash';

  @override
  String syncPending(int n) {
    return 'Yuborishni kutmoqda: $n';
  }

  @override
  String get syncAllSent => 'Barcha o\'zgarishlar yuborildi';

  @override
  String syncFailed(String error) {
    return 'Server rad etdi: $error';
  }

  @override
  String get syncNow => 'Hozir yuborish';

  @override
  String get profileRoles => 'Rollar';

  @override
  String get profileDepartment => 'Bo\'lim';

  @override
  String get organization1c => '1C tashkiloti';

  @override
  String get langRu => 'Ruscha';

  @override
  String get langUz => 'O\'zbekcha';

  @override
  String get langSystem => 'Tizimdagidek';

  @override
  String get anSales => 'Savdo';

  @override
  String get anPlanCard => 'Reja bajarilishi';

  @override
  String get anPlanNone => 'Reja yo\'q';

  @override
  String get anPlanOnTrack => 'Reja bo\'yicha';

  @override
  String get anPlanClose => 'Sur\'atga yaqin';

  @override
  String get anPlanBehind => 'Sur\'atdan orqada';

  @override
  String get anPlanDone => 'Reja bajarildi ✓';

  @override
  String get anPlanAlmost => 'Deyarli bajarildi';

  @override
  String get anPlanFailed => 'Reja bajarilmadi';

  @override
  String anFactFor(String month) {
    return '$month uchun fakt';
  }

  @override
  String get anPace => 'Joriy sur\'at';

  @override
  String anPerDay(String value) {
    return '$value/kun';
  }

  @override
  String get anRequired => 'Kuniga kerak';

  @override
  String get anForecast => 'Oy oxiriga prognoz';

  @override
  String get anShouldBe => 'bo\'lishi kerak';

  @override
  String anPlanOf(String fact, String plan) {
    return '$plan dan $fact';
  }

  @override
  String get anConversion => 'Tashrif konversiyasi';

  @override
  String get anRevenuePerOutlet => 'Savdo nuqtasiga tushum';

  @override
  String get anAkbPerAgent => 'Agentga AKB';

  @override
  String anTarget(String value) {
    return 'maqsad $value';
  }

  @override
  String anOfTarget(String pct) {
    return 'maqsadning $pct';
  }

  @override
  String get anVisitsNoOrder => 'Buyurtmasiz tashriflar';

  @override
  String anVisitsNoOrderOf(String n) {
    return 'savdo vakillarining $n ta tashrifidan';
  }

  @override
  String anAgentsAttention(int bad, int all) {
    return '$all savdo vakilidan $bad tasi — e\'tibor';
  }

  @override
  String get anCritical => 'Jiddiy';

  @override
  String get anRisk => 'Xavf';

  @override
  String get anNormal => 'Me\'yorda';

  @override
  String get anRepublic => 'Respublika';

  @override
  String get anMyRegions => 'Mening hududlarim';

  @override
  String get anDirections => 'Yo\'nalishlar';

  @override
  String get anRegions => 'Hududlar';

  @override
  String get anAgents => 'Agentlar';

  @override
  String get anPlanKg => 'Reja, kg';

  @override
  String get anFact => 'Fakt';

  @override
  String get anForecastShort => 'Prognoz';

  @override
  String get anStrike => 'Strayk';

  @override
  String get anExecution => 'Bajarilish';

  @override
  String get anSameDays => 'O\'tgan oyning shu kunlariga';

  @override
  String get anByDays => 'Kunlar bo\'yicha savdo';

  @override
  String get anCategories => 'Kategoriyalar';

  @override
  String anSkuSold(int sold, int total) {
    return '$total SKU dan $sold tasi sotilmoqda';
  }

  @override
  String anWeightShare(String pct) {
    return 'umumiy vaznning $pct';
  }

  @override
  String anSilent(int n) {
    return 'jim $n';
  }

  @override
  String anLost(int n) {
    return 'yo\'qolgan $n';
  }

  @override
  String get anTeam => 'Jamoa';

  @override
  String get anPlanFactMonths => 'Oylar bo\'yicha reja va fakt';

  @override
  String get anRevenueMix => 'Tushum tarkibi';

  @override
  String anSupervisor(String name) {
    return 'Supervayzer: $name';
  }

  @override
  String anDealer(String name) {
    return 'diler: $name';
  }

  @override
  String get anVacancy => 'vakansiya';

  @override
  String get anNoRemarks => 'Izohlar yo\'q';

  @override
  String get anSearchAgents => 'Ism, ID yoki hudud';

  @override
  String get anFilterAll => 'Hammasi';

  @override
  String get anFilterPlanOk => 'Rejani bajarmoqda';

  @override
  String get anFilterBelow => 'Rejadan past';

  @override
  String get anFilterNoPlan => 'Rejasiz';

  @override
  String get anFilterRemarks => 'Izohlar bilan';

  @override
  String get anFilterVacancy => 'Vakansiyalar';

  @override
  String get anSortExecution => 'Bajarilish bo\'yicha';

  @override
  String get anSortFact => 'Fakt bo\'yicha, kg';

  @override
  String get anSortRevenue => 'Tushum bo\'yicha';

  @override
  String get anSortName => 'Ism bo\'yicha';

  @override
  String anShown(int n, int all) {
    return '$all savdo vakilidan $n tasi ko\'rsatilgan';
  }

  @override
  String anAgentNoPlan(String fact) {
    return 'Fakt $fact · Linko\'da oylik reja yo\'q';
  }

  @override
  String get anRevenue => 'Tushum';

  @override
  String get anVisits => 'Tashriflar';

  @override
  String anWithOrder(int n) {
    return '$n ta buyurtma bilan';
  }

  @override
  String get anOrders => 'Buyurtmalar';

  @override
  String get anTempo => 'Sur\'at';

  @override
  String anCategoriesN(int n) {
    return '$n ta kategoriya';
  }

  @override
  String anMedian(String value) {
    return 'hudud medianasi $value';
  }

  @override
  String get anSumPerVisit => 'Tashrifdan so\'m';

  @override
  String get anAvgCheck => 'O\'rtacha chek';

  @override
  String get anKgPerOutlet => 'Nuqtaga vazn';

  @override
  String get anFindings => 'Nima topildi';

  @override
  String get anStores => 'Oy savdo nuqtalari';

  @override
  String get anSilentMarkets => 'Hali sotib olmagan';

  @override
  String get anNewMarkets => 'Yangi nuqtalar';

  @override
  String anPrevMonth(String value) {
    return 'o\'tgan oy $value';
  }

  @override
  String get anStore => 'Savdo nuqtasi';

  @override
  String get anStoreAgents => 'Sotgan';

  @override
  String get anProducts => 'Artikullar';

  @override
  String get anCompare => 'Davrlarni taqqoslash';

  @override
  String anCompareWith(String label) {
    return '$label ga nisbatan';
  }

  @override
  String get anPeriodWeek => 'Hafta';

  @override
  String get anPeriodMonth => 'Oy';

  @override
  String get anPeriodQuarter => 'Chorak';

  @override
  String get anPeriodYear => 'Yil';

  @override
  String get anModSales => 'Savdo';

  @override
  String get anModFinance => 'Moliya';

  @override
  String get anModProduction => 'Ishlab chiqarish';

  @override
  String get anModHr => 'Xodimlar';

  @override
  String get anNoData => 'ma\'lumot yo\'q';

  @override
  String get anIncomplete => 'Ma\'lumotlar to\'liq emas';

  @override
  String get anSlow => 'Hisoblanmoqda — bir necha soniyagacha';

  @override
  String get anProductionToday => 'Bugungi ishlab chiqarish';

  @override
  String get anCompletion => 'Reja bajarilishi';

  @override
  String get anDefect => 'Brak';

  @override
  String get anActiveOrders => 'Ishdagi topshiriqlar';

  @override
  String get anFindingsTitle => 'Analitik topilmalari';

  @override
  String anFindingsLine(int critical, int warning, int info) {
    return '$critical jiddiy · $warning e\'tibor · $info ma\'lumot uchun';
  }

  @override
  String get anMonthPick => 'Oy';

  @override
  String get anOpenAll => 'Ochish';

  @override
  String anScopeNote(String scope) {
    return 'Savdo qamrovingiz: $scope';
  }

  @override
  String get crmSearch => 'Nuqta nomi, manzili yoki ID';

  @override
  String get crmAttAll => 'Hammasi';

  @override
  String get crmAttDecline => 'Savdo pasaygan';

  @override
  String get crmAttLost => 'Sotib olishni to\'xtatgan';

  @override
  String get crmAttNotVisited => 'Uzoq vaqt borilmagan';

  @override
  String get crmAttNew => 'Xaridsiz';

  @override
  String get crmStatusActive => 'Faol';

  @override
  String get crmStatusProblem => 'Muammoli';

  @override
  String get crmStatusInactive => 'Nofaol';

  @override
  String get crmPriorityLow => 'Past';

  @override
  String get crmPriorityMedium => 'O\'rta';

  @override
  String get crmPriorityHigh => 'Yuqori';

  @override
  String get crmPriorityUrgent => 'Shoshilinch';

  @override
  String get crmSortSales => '30 kunlik savdo bo\'yicha';

  @override
  String get crmSortTrend => 'Ko\'proq pasayganlar';

  @override
  String get crmSortLastVisit => 'Uzoq vaqt tashrifsiz';

  @override
  String get crmSortName => 'Nomi bo\'yicha';

  @override
  String crmSales30(String value) {
    return '30 kunda $value';
  }

  @override
  String crmLastVisit(String date) {
    return 'tashrif $date';
  }

  @override
  String get crmNoAccess =>
      'Mijozlar ro\'yxati Sales Base\'da yuritiladi. Siz hali Sales Base\'ga qo\'shilmagansiz — savdo vakillaringiz nuqtalari «Tahlil»dagi savdo vakili kartasidan ochiladi.';

  @override
  String get crmMonthToDate => 'Oy boshidan';

  @override
  String get crmOrders30 => 'Buyurtmalar, 30 kun';

  @override
  String get crmVisits30 => 'Tashriflar, 30 kun';

  @override
  String get crmTrend => 'Trend';

  @override
  String get crmResponsible => 'Mas\'ul savdo vakili';

  @override
  String get crmContact => 'Aloqa';

  @override
  String get crmPhone => 'Telefon';

  @override
  String get crmNote => 'Izoh';

  @override
  String get crmTarget => 'Oylik reja';

  @override
  String get crmMonths => 'Oylar bo\'yicha jo\'natmalar';

  @override
  String get crmOrders => 'Buyurtmalar';

  @override
  String get crmVisits => 'Tashriflar';

  @override
  String get crmHistory => 'O\'zgarishlar tarixi';

  @override
  String get crmMonthSales => 'Oylik savdo';

  @override
  String get crmEdit => 'O\'zgartirish';

  @override
  String get crmEditTitle => 'Nuqta kartasi';

  @override
  String get crmSaved => 'Saqlandi';

  @override
  String get crmStatus => 'Holat';

  @override
  String get crmPriority => 'Ustuvorlik';

  @override
  String get crmLinkoReadonly =>
      'Linko ma\'lumotlari faqat o\'qish uchun: Sales Base kartasi o\'zgaradi (holat, ustuvorlik, reja, kontaktlar).';

  @override
  String get actionSave => 'Saqlash';

  @override
  String get auditUpdated => 'Karta o\'zgartirildi';

  @override
  String get auditAgent => 'Agent almashtirildi';

  @override
  String get crm30d => '30 kunda';

  @override
  String get crm90d => '90 kunda';

  @override
  String anWas(String value) {
    return 'edi $value';
  }

  @override
  String get finTabOverview => 'Sharh';

  @override
  String get finTabPnl => 'Daromad va xarajatlar';

  @override
  String get finTabCash => 'Pul';

  @override
  String get finTabDebts => 'Qarzlar';

  @override
  String get finTabAnomalies => 'Og\'ishlar';

  @override
  String get finPeriodMtd => 'Shu oy';

  @override
  String get finPeriodPrev => 'O\'tgan oy';

  @override
  String get finPeriodQuarter => 'Chorak';

  @override
  String get finPeriodYear => 'Yil boshidan';

  @override
  String get finAllOrgs => 'Barcha tashkilotlar';

  @override
  String get finDemo => '1C test manbasining demo ma\'lumotlari';

  @override
  String finDataThrough(String date) {
    return '$date gacha provodkalar';
  }

  @override
  String get finGrossProfit => 'Yalpi foyda';

  @override
  String finVsPrev(String value) {
    return 'o\'tgan davrga $value';
  }

  @override
  String get finMonths => '12 oy';

  @override
  String get finCashTotal => 'Hisob va kassadagi pul';

  @override
  String get finReceivables => 'Bizga qarz';

  @override
  String get finPayables => 'Biz qarzmiz';

  @override
  String get finInventory => 'Zaxiralar';

  @override
  String get finExpenseGrowth => 'O\'sgan xarajatlar';

  @override
  String get finExpensesByAccount => 'Moddalar bo\'yicha xarajatlar';

  @override
  String get finExpensesByDepartment => 'Bo\'linmalar bo\'yicha xarajatlar';

  @override
  String get finInflow => 'Tushumlar';

  @override
  String get finOutflow => 'To\'lovlar';

  @override
  String get finBalances => 'Qoldiqlar';

  @override
  String get finOverdue => 'Muddati o\'tgan';

  @override
  String get finAdvances => 'Avanslar';

  @override
  String get finBucket_not_due => 'Muddat kelmagan';

  @override
  String get finBucket_1_30 => '1–30 kun kechikish';

  @override
  String get finBucket_31_60 => '31–60 kun';

  @override
  String get finBucket_61_90 => '61–90 kun';

  @override
  String get finBucket_over_90 => '90 kundan ko\'p';

  @override
  String get finBucket_no_term => 'Muddat belgilanmagan';

  @override
  String finOverdueDays(int n) {
    return '$n kungacha kechikish';
  }

  @override
  String get finChecks => 'Nima tekshirildi';

  @override
  String get finNoAnomalies => 'Og\'ishlar topilmadi';

  @override
  String get finEntries => 'Provodkalar';

  @override
  String finEntriesTotal(int n, String sum) {
    return '$sum ga $n ta provodka';
  }

  @override
  String get finDocument => '1C hujjati';

  @override
  String get finPosted => 'O\'tkazilgan';

  @override
  String get finNotPosted => 'O\'tkazilmagan';

  @override
  String get finCounterparty => 'Kontragent';

  @override
  String get finContract => 'Shartnoma';

  @override
  String get finWarehouse => 'Ombor';

  @override
  String get finPurpose => 'Maqsad';

  @override
  String get finLines => 'Qatorlar';

  @override
  String get finVat => 'QQS';

  @override
  String get finAccounts => 'Hisoblar (AOV)';

  @override
  String get finOpening => 'Boshi';

  @override
  String get finTurnover => 'Aylanmalar';

  @override
  String get finClosing => 'Oxiri';

  @override
  String get finDebit => 'Dt';

  @override
  String get finCredit => 'Kt';

  @override
  String get prTabOverview => 'Sharh';

  @override
  String get prTabOrders => 'Topshiriqlar';

  @override
  String get prTabShifts => 'Smenalar';

  @override
  String get prGood => 'Yaroqli mahsulot';

  @override
  String get prPlan => 'Reja';

  @override
  String get prOpenShifts => 'Ochiq smenalar';

  @override
  String get prClosedShifts => 'Tasdiqlashni kutmoqda';

  @override
  String get prNormDev => 'Me\'yordan og\'ish';

  @override
  String get prByProduct => 'Mahsulotlar bo\'yicha';

  @override
  String get prIssues => 'Muammolar';

  @override
  String get prStockValue => 'Ombordagi xomashyo';

  @override
  String get prExpiring => '30 kunda muddati tugaydi';

  @override
  String get prExpired => 'Muddati o\'tgan';

  @override
  String get prBelowMin => 'Minimumdan past';

  @override
  String get prOverdueOrders => 'Muddati o\'tgan topshiriqlar';

  @override
  String get prLowDays => 'Eng kam zaxira';

  @override
  String prDaysLeft(String n) {
    return '$n kun';
  }

  @override
  String get prStatus_Draft => 'Qoralama';

  @override
  String get prStatus_Released => 'Chiqarilgan';

  @override
  String get prStatus_InProgress => 'Jarayonda';

  @override
  String get prStatus_Completed => 'Yopilgan';

  @override
  String get prStatus_Cancelled => 'Bekor qilingan';

  @override
  String get prFilterOpen => 'Ochiq';

  @override
  String get prFilterDone => 'Yopilgan';

  @override
  String get prFilterAll => 'Hammasi';

  @override
  String prPlanned(String qty, String date) {
    return 'Reja $qty · $date ga';
  }

  @override
  String get prOutput => 'Ishlab chiqarish';

  @override
  String get prMaterials => 'Xomashyo va materiallar';

  @override
  String prRequired(String v) {
    return 'kerak $v';
  }

  @override
  String prIssued(String v) {
    return 'berilgan $v';
  }

  @override
  String prLeft(String v) {
    return 'berish qoldi $v';
  }

  @override
  String prAvailable(String v) {
    return 'omborda $v';
  }

  @override
  String get prOverconsumed => 'ortiqcha sarf';

  @override
  String get prHistory => 'Tarix';

  @override
  String get prRelease => 'Topshiriqni chiqarish';

  @override
  String get prComplete => 'Topshiriqni yopish';

  @override
  String get prCancel => 'Topshiriqni bekor qilish';

  @override
  String get prReceive => 'Mahsulotni kirim qilish';

  @override
  String prOutputQty(String unit) {
    return 'Haqiqiy ishlab chiqarish, $unit';
  }

  @override
  String get prReason => 'Sabab';

  @override
  String get prConfirmTitle => 'Operatsiyani tasdiqlang';

  @override
  String get prConfirmRelease =>
      'Topshiriq ishga yuboriladi; xomashyo yetishmasligi belgilanadi, ta\'minot xabardor qilinadi.';

  @override
  String prConfirmComplete(String qty) {
    return '$qty ishlab chiqarish qayd etiladi va me\'yor bo\'yicha sarf hisoblanadi. Yopishni bekor qilib bo\'lmaydi.';
  }

  @override
  String get prConfirmReceive => 'Mahsulot omborga kirim qilinadi.';

  @override
  String prDone(String what) {
    return 'Tayyor: $what';
  }

  @override
  String get prConflict =>
      'Topshiriq o\'zgartirildi — ma\'lumotlar yangilandi, tekshirib qaytadan urinib ko\'ring.';

  @override
  String prShift(int n, String date) {
    return '$n-smena · $date';
  }

  @override
  String get prShiftStatus_Draft => 'Kiritish';

  @override
  String get prShiftStatus_Closed => 'Tasdiqlashni kutmoqda';

  @override
  String get prShiftStatus_Approved => 'Tasdiqlangan';

  @override
  String get prShiftStatus_Superseded => 'Almashtirilgan';

  @override
  String get prShiftStatus_Cancelled => 'Bekor qilingan';

  @override
  String get whTabStock => 'Qoldiqlar';

  @override
  String get whTabMoves => 'Harakatlar';

  @override
  String get whSearch => 'Artikul, nom yoki shtrix-kod';

  @override
  String get whFilterAll => 'Hammasi';

  @override
  String get whFilterLow => 'Minimumdan past';

  @override
  String get whFilterReserved => 'Rezervda';

  @override
  String get whFilterEmpty => 'Qoldiq yo\'q';

  @override
  String get whFilterExpiring => 'Muddati tugayapti';

  @override
  String get whFilterExpired => 'Muddati o\'tgan';

  @override
  String get whOnHand => 'Qoldiq';

  @override
  String get whReserved => 'Rezerv';

  @override
  String get whAvailable => 'Bo\'sh';

  @override
  String get whMin => 'Minimum';

  @override
  String get whReservations => 'Arizalar bo\'yicha rezervlar';

  @override
  String get whMovements => 'Oxirgi harakatlar';

  @override
  String get whOperation => 'Operatsiya';

  @override
  String get whOpReceipt => 'Kirim';

  @override
  String get whOpIssue => 'Bo\'limga berish';

  @override
  String get whOpWriteOff => 'Hisobdan chiqarish';

  @override
  String get whItem => 'Pozitsiya';

  @override
  String get whPickItem => 'Pozitsiyani tanlang';

  @override
  String whQuantity(String unit) {
    return 'Miqdor, $unit';
  }

  @override
  String get whUnitCost => 'Birlik narxi, so\'m';

  @override
  String get whDepartment => 'Bo\'lim';

  @override
  String get whReceiver => 'Kim oldi';

  @override
  String get whComment => 'Izoh';

  @override
  String get whReference => 'Yuk xati';

  @override
  String get whReasonRequired => 'Hisobdan chiqarish sababini kiriting';

  @override
  String get whPost => 'O\'tkazish';

  @override
  String whConfirm(String op, String item, String qty) {
    return '$op: $item — $qty. Ombordagi qoldiq o\'zgaradi.';
  }

  @override
  String get whPosted => 'Hujjat o\'tkazildi';

  @override
  String get whNoPosts =>
      'Omborda operatsiyalarni faqat shu omborning omborchisi o\'tkaza oladi.';

  @override
  String get whWarehouse => 'Ombor';

  @override
  String get whOnline =>
      'Ombor operatsiyalari faqat server bilan aloqa bo\'lganda o\'tkaziladi.';

  @override
  String get mvReceipt => 'Kirim';

  @override
  String get mvIssue => 'Berish';

  @override
  String get mvWriteOff => 'Hisobdan chiqarish';

  @override
  String get mvAdjustment => 'Inventarizatsiya';

  @override
  String get mvTransfer => 'Ko\'chirish';

  @override
  String get mvReversal => 'Storno';

  @override
  String get mvProduction => 'Ishlab chiqarish';

  @override
  String get rqScopeMy => 'Meniki';

  @override
  String get rqScopeDepartment => 'Bo\'lim';

  @override
  String get rqScopeAll => 'Hammasi';

  @override
  String rqItems(int n) {
    return '$n poz.';
  }

  @override
  String rqDesired(String date) {
    return '$date ga kerak';
  }
}
