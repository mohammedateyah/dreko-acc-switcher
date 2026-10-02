using System.Globalization;
using System.Windows;

namespace DrekoAccSwitcher.Services;

public static class Localization
{
    private static readonly Dictionary<string, (string English, string Arabic)> Strings = new()
    {
        ["launchers"] = ("Launchers", "المنصات"),
        ["home"] = ("Home", "الرئيسية"),
        ["homeTitle"] = ("Welcome to Dreko", "مرحباً بك في Dreko"),
        ["welcomeTitle"] = ("Welcome to Dreko", "مرحباً بك في Dreko"),
        ["welcomeHeading"] = ("This app is completely free", "هذا التطبيق مجاني بالكامل"),
        ["welcomeRightsPrefix"] = ("All rights reserved to ", "جميع الحقوق محفوظة لـ "),
        ["welcomeRightsSuffix"] = (".", "."),
        ["welcomeVisitPrompt"] = ("For more apps, visit our website:", "لزيارة موقعنا للمزيد من التطبيقات، اضغط هنا:"),
        ["welcomeWebsiteLink"] = ("Visit DrekoStudio", "زيارة موقع DrekoStudio"),
        ["welcomeContinue"] = ("Continue", "متابعة"),
        ["websiteOpenFailed"] = ("Could not open the website: {0}", "تعذّر فتح الموقع: {0}"),
        ["homeDescription"] = ("Manage your launcher accounts in one place and switch between saved sessions without repeatedly entering your credentials.",
            "أدر حسابات منصات الألعاب في مكان واحد، وبدّل بين الجلسات المحفوظة دون الحاجة إلى إدخال بيانات الدخول كل مرة."),
        ["totalAccounts"] = ("Saved accounts", "الحسابات المحفوظة"),
        ["unavailable"] = ("—", "—"),
        ["updateStatus"] = ("Update status", "حالة التحديث"),
        ["updateChecking"] = ("Checking for updates…", "جارٍ التحقق من التحديثات…"),
        ["updateAvailable"] = ("Version {0} is available.", "يتوفر تحديث الإصدار {0}."),
        ["upToDate"] = ("You’re using the latest version.", "أنت تستخدم أحدث إصدار."),
        ["noRelease"] = ("No release has been published yet.", "لم يتم نشر أي إصدار بعد."),
        ["updateCheckFailed"] = ("Could not check for updates. Check your internet connection and try again later.",
            "تعذّر التحقق من التحديثات. تحقّق من اتصال الإنترنت وحاول مجدداً لاحقاً."),
        ["howToUse"] = ("How it works", "طريقة الاستخدام"),
        ["howToUseStepOne"] = ("Choose a launcher and sign in to your account.", "اختر منصة وسجّل الدخول إلى حسابك."),
        ["howToUseStepTwo"] = ("Save the current account to keep its launcher session on this PC.", "احفظ الحساب الحالي للاحتفاظ بجلسته على هذا الجهاز."),
        ["howToUseStepThree"] = ("Select a saved account and click Switch to reopen its session.", "اختر حساباً محفوظاً واضغط «تبديل» لفتح جلسته."),
        ["privacyNoteTitle"] = ("Your data stays on your device", "ملاحظة حول خصوصية بياناتك"),
        ["privacyNoteText"] = ("Dreko does not send your account information to Dreko servers. Saved accounts and sessions stay in the app's data folder on this device. Keep your device secure and do not share that folder with others.",
            "لا يرسل Dreko معلومات حساباتك إلى خوادمه. تبقى الحسابات والجلسات المحفوظة في مجلد بيانات التطبيق على هذا الجهاز. احرص على حماية جهازك ولا تشارك هذا المجلد مع الآخرين."),
        ["openData"] = ("Open data folder", "فتح مجلد البيانات"),
        ["settings"] = ("Settings", "الإعدادات"),
        ["launch"] = ("Launch", "تشغيل"),
        ["addNew"] = ("Add new", "إضافة حساب"),
        ["saveAccount"] = ("Save current account", "حفظ الحساب الحالي"),
        ["searchAccounts"] = ("Search accounts", "ابحث عن حساب"),
        ["switch"] = ("Switch", "تبديل"),
        ["rename"] = ("Rename", "إعادة تسمية"),
        ["forget"] = ("Forget", "نسيان الحساب"),
        ["rightClickHint"] = ("Right-click to rename or forget.", "انقر بزر الفأرة الأيمن لإعادة التسمية أو نسيان الحساب."),
        ["noAccounts"] = ("No accounts yet", "لا توجد حسابات بعد"),
        ["emptyHint"] = ("Sign in to the launcher with Remember me / Remember password, then click Save current account. For Steam, remembered logins are listed automatically.",
            "سجّل الدخول إلى المنصة مع تفعيل تذكّرني أو تذكّر كلمة المرور، ثم اختر «حفظ الحساب الحالي». تظهر حسابات Steam المحفوظة تلقائياً."),
        ["selectLauncher"] = ("Select a launcher", "اختر منصة"),
        ["steamHint"] = ("Steam accounts already remembered on this PC appear here. Click one to switch.",
            "تظهر هنا حسابات Steam المحفوظة على هذا الجهاز. اختر حساباً للتبديل إليه."),
        ["saveHint"] = ("Save the account you are logged into, then click a card to switch.",
            "احفظ الحساب الذي سجلت الدخول إليه، ثم اختر بطاقته للتبديل إليه."),
        ["installed"] = ("Installed", "مثبّت"),
        ["notFound"] = ("Not found", "غير موجود"),
        ["ready"] = ("Ready.", "جاهز."),
        ["working"] = ("Working…", "جارٍ التنفيذ…"),
        ["switched"] = ("Switched and launched.", "تم التبديل وتشغيل المنصة."),
        ["launcherOpened"] = ("Launcher opened. Sign in, then Save current account.", "فُتحت المنصة. سجّل الدخول ثم احفظ الحساب الحالي."),
        ["epicAddReady"] = ("Epic is open. Sign out from its profile menu to add another account.", "Epic مفتوح. سجّل الخروج من قائمة الحساب لإضافة حساب آخر."),
        ["epicSignOutRequired"] = ("Epic does not provide a supported way for Dreko to sign out automatically. In Epic, open the profile menu in the top-right and choose Sign Out, then sign in to the new account. Your saved Dreko accounts are preserved.",
            "لا يوفّر Epic طريقة مدعومة لتسجيل الخروج تلقائياً من Dreko. في Epic، افتح قائمة الحساب أعلى اليمين واختر «تسجيل الخروج»، ثم سجّل الدخول بالحساب الجديد. حساباتك المحفوظة في Dreko ستبقى كما هي."),
        ["accountSaved"] = ("Current account saved.", "تم حفظ الحساب الحالي."),
        ["forgotten"] = ("Account forgotten.", "تم نسيان الحساب."),
        ["launcherStarted"] = ("Launcher started.", "تم تشغيل المنصة."),
        ["launcherReady"] = ("{0} ready.", "{0} جاهز."),
        ["launcherMissing"] = ("{0} executable was not found. You can still save paths after installing it.",
            "لم يتم العثور على ملف تشغيل {0}. يمكنك حفظ الحساب بعد تثبيت المنصة."),
        ["listFailed"] = ("Could not load accounts: {0}", "تعذّر تحميل الحسابات: {0}"),
        ["forgetPrompt"] = ("Remove '{0}' from Dreko? This does not delete the real game account.",
            "هل تريد إزالة '{0}' من Dreko؟ لن يؤدي ذلك إلى حذف حساب اللعبة."),
        ["forgetTitle"] = ("Forget account", "نسيان الحساب"),
        ["renameTitle"] = ("Rename account", "إعادة تسمية الحساب"),
        ["displayName"] = ("Display name", "الاسم المعروض"),
        ["saveTitle"] = ("Save current account", "حفظ الحساب الحالي"),
        ["nameAccount"] = ("Name this account", "أدخل اسماً لهذا الحساب"),
        ["appTitle"] = ("Dreko Acc Switcher", "مبدّل حسابات Dreko"),
        ["alreadyRunning"] = ("Dreko Acc Switcher is already running. Only one instance can be open at a time.",
            "تطبيق Dreko مفتوح بالفعل. يمكن تشغيل نسخة واحدة فقط في كل مرة."),
        ["cancel"] = ("Cancel", "إلغاء"),
        ["save"] = ("Save", "حفظ"),
        ["settingsTitle"] = ("Application settings", "إعدادات التطبيق"),
        ["language"] = ("Language", "اللغة"),
        ["english"] = ("English", "الإنجليزية"),
        ["arabic"] = ("العربية", "العربية"),
        ["launchOnStartup"] = ("Start Dreko when Windows starts", "تشغيل Dreko عند بدء Windows"),
        ["closeToTray"] = ("Keep running in the notification area when closed", "إبقاء التطبيق يعمل في منطقة الإشعارات عند إغلاقه"),
        ["trayHint"] = ("When enabled, closing the window hides Dreko in the system tray. Use its tray menu to reopen or exit.",
            "عند التفعيل، يؤدي إغلاق النافذة إلى إخفاء Dreko في منطقة الإشعارات. استخدم قائمة الأيقونة لإعادة فتحه أو إنهائه."),
        ["open"] = ("Open Dreko", "فتح Dreko"),
        ["exit"] = ("Exit", "إنهاء"),
        ["languageRestart"] = ("Language changed.", "تم تغيير اللغة."),
        ["saveSettingsFailed"] = ("Could not save settings: {0}", "تعذّر حفظ الإعدادات: {0}"),
        ["search"] = ("Search", "بحث"),
        ["clearSearch"] = ("CLEAR", "مسح"),
        ["lastUsed"] = ("Last used", "آخر استخدام"),
        ["rememberedByLauncher"] = ("Remembered by launcher", "محفوظ في المنصة"),
        ["savedLocally"] = ("Saved locally", "محفوظ محلياً")
    };

    public static event EventHandler? Changed;
    public static string Language => SettingsStore.Current.Language;
    public static bool IsArabic => Language == "ar";
    public static System.Windows.FlowDirection FlowDirection =>
        IsArabic ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;
    public static string Text(string key)
    {
        if (!Strings.TryGetValue(key, out var value))
            throw new KeyNotFoundException($"No localized text is defined for '{key}'.");
        return IsArabic ? value.Arabic : value.English;
    }

    public static string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Text(key), arguments);

    public static void ApplyCulture()
    {
        var culture = CultureInfo.GetCultureInfo(IsArabic ? "ar" : "en");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public static void NotifyChanged() => Changed?.Invoke(null, EventArgs.Empty);
}
