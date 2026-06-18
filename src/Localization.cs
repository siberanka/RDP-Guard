using System;
using System.Collections.Generic;
using System.Linq;

namespace RDPGuard
{
    internal sealed class LanguageOption
    {
        public LanguageOption(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }

        public string Code { get; }
        public string DisplayName { get; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    internal static class Localization
    {
        private static readonly List<LanguageOption> LanguageList = new List<LanguageOption>
        {
            new LanguageOption("en", "English"),
            new LanguageOption("tr", "T\u00fcrk\u00e7e"),
            new LanguageOption("de", "Deutsch"),
            new LanguageOption("ru", "\u0420\u0443\u0441\u0441\u043a\u0438\u0439"),
            new LanguageOption("fr", "Fran\u00e7ais"),
            new LanguageOption("es", "Espa\u00f1ol"),
            new LanguageOption("zh", "\u4e2d\u6587")
        };

        private static readonly Dictionary<string, Dictionary<string, string>> Texts =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["AppTitle"] = "RDP Guard",
                    ["Threshold"] = "Threshold (>=)",
                    ["Interval"] = "Interval (min)",
                    ["Language"] = "Language",
                    ["StartWithWindows"] = "Start with Windows",
                    ["Start"] = "Start",
                    ["Stop"] = "Stop",
                    ["CheckNow"] = "Check now",
                    ["Save"] = "Save",
                    ["Unblock"] = "Remove block",
                    ["Whitelist"] = "Whitelist",
                    ["BlockedIps"] = "Blocked IPs",
                    ["Log"] = "Log",
                    ["Ip"] = "Blocked IP",
                    ["Date"] = "Date",
                    ["Count"] = "Count",
                    ["Rule"] = "Rule",
                    ["Active"] = "Active",
                    ["Stopped"] = "Stopped",
                    ["Open"] = "Open",
                    ["Hide"] = "Hide",
                    ["ToggleProtection"] = "Toggle protection",
                    ["StopProtection"] = "Stop protection",
                    ["StartProtection"] = "Start protection",
                    ["Exit"] = "Exit",
                    ["CloseTitle"] = "RDP Guard is closing",
                    ["CloseMessage"] = "The program can keep checking audit failures in the background.",
                    ["MinimizeToTray"] = "Minimize to tray",
                    ["CloseProgram"] = "Close program",
                    ["SettingsSaved"] = "Settings saved.",
                    ["LastCheck"] = "Last check: {0} events, {1} IPs, {2} new blocks"
                },
                ["tr"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Threshold"] = "E\u015fik (>=)",
                    ["Interval"] = "Aral\u0131k (dk)",
                    ["Language"] = "Dil",
                    ["StartWithWindows"] = "Windows ile ba\u015flat",
                    ["Start"] = "Ba\u015flat",
                    ["Stop"] = "Durdur",
                    ["CheckNow"] = "Kontrol et",
                    ["Save"] = "Kaydet",
                    ["Unblock"] = "Engeli kald\u0131r",
                    ["Whitelist"] = "Beyaz liste",
                    ["BlockedIps"] = "Engellenen IP'ler",
                    ["Log"] = "Log",
                    ["Ip"] = "Engellenen IP",
                    ["Date"] = "Tarih",
                    ["Count"] = "Say\u0131",
                    ["Rule"] = "Kural",
                    ["Active"] = "Aktif",
                    ["Stopped"] = "Durduruldu",
                    ["Open"] = "A\u00e7",
                    ["Hide"] = "Gizle",
                    ["ToggleProtection"] = "Koruma durumunu de\u011fi\u015ftir",
                    ["StopProtection"] = "Korumay\u0131 durdur",
                    ["StartProtection"] = "Korumay\u0131 ba\u015flat",
                    ["Exit"] = "Kapat",
                    ["CloseTitle"] = "RDP Guard kapat\u0131l\u0131yor",
                    ["CloseMessage"] = "Program arka planda audit failure kontrol\u00fcne devam edebilir.",
                    ["MinimizeToTray"] = "Simge durumuna k\u00fc\u00e7\u00fclt",
                    ["CloseProgram"] = "Program\u0131 kapat",
                    ["SettingsSaved"] = "Ayarlar kaydedildi.",
                    ["LastCheck"] = "Son kontrol: {0} olay, {1} IP, {2} yeni engel"
                },
                ["de"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Threshold"] = "Schwelle (>=)",
                    ["Interval"] = "Intervall (Min.)",
                    ["Language"] = "Sprache",
                    ["StartWithWindows"] = "Mit Windows starten",
                    ["Start"] = "Starten",
                    ["Stop"] = "Stoppen",
                    ["CheckNow"] = "Jetzt pr\u00fcfen",
                    ["Save"] = "Speichern",
                    ["Unblock"] = "Blockierung entfernen",
                    ["Whitelist"] = "Whitelist",
                    ["BlockedIps"] = "Blockierte IPs",
                    ["Log"] = "Protokoll",
                    ["Ip"] = "Blockierte IP",
                    ["Date"] = "Datum",
                    ["Count"] = "Anzahl",
                    ["Rule"] = "Regel",
                    ["Active"] = "Aktiv",
                    ["Stopped"] = "Gestoppt",
                    ["Open"] = "\u00d6ffnen",
                    ["Hide"] = "Ausblenden",
                    ["StopProtection"] = "Schutz stoppen",
                    ["StartProtection"] = "Schutz starten",
                    ["Exit"] = "Beenden",
                    ["CloseTitle"] = "RDP Guard wird geschlossen",
                    ["CloseMessage"] = "Das Programm kann Audit-Fehler im Hintergrund weiter pr\u00fcfen.",
                    ["MinimizeToTray"] = "In den Infobereich minimieren",
                    ["CloseProgram"] = "Programm beenden",
                    ["SettingsSaved"] = "Einstellungen gespeichert.",
                    ["LastCheck"] = "Letzte Pr\u00fcfung: {0} Ereignisse, {1} IPs, {2} neue Blockierungen"
                },
                ["ru"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Threshold"] = "\u041f\u043e\u0440\u043e\u0433 (>=)",
                    ["Interval"] = "\u0418\u043d\u0442\u0435\u0440\u0432\u0430\u043b (\u043c\u0438\u043d)",
                    ["Language"] = "\u042f\u0437\u044b\u043a",
                    ["StartWithWindows"] = "\u0417\u0430\u043f\u0443\u0441\u043a \u0441 Windows",
                    ["Start"] = "\u0417\u0430\u043f\u0443\u0441\u0442\u0438\u0442\u044c",
                    ["Stop"] = "\u041e\u0441\u0442\u0430\u043d\u043e\u0432\u0438\u0442\u044c",
                    ["CheckNow"] = "\u041f\u0440\u043e\u0432\u0435\u0440\u0438\u0442\u044c",
                    ["Save"] = "\u0421\u043e\u0445\u0440\u0430\u043d\u0438\u0442\u044c",
                    ["Unblock"] = "\u0421\u043d\u044f\u0442\u044c \u0431\u043b\u043e\u043a",
                    ["Whitelist"] = "\u0411\u0435\u043b\u044b\u0439 \u0441\u043f\u0438\u0441\u043e\u043a",
                    ["BlockedIps"] = "\u0417\u0430\u0431\u043b\u043e\u043a\u0438\u0440\u043e\u0432\u0430\u043d\u043d\u044b\u0435 IP",
                    ["Log"] = "\u0416\u0443\u0440\u043d\u0430\u043b",
                    ["Ip"] = "IP",
                    ["Date"] = "\u0414\u0430\u0442\u0430",
                    ["Count"] = "\u0427\u0438\u0441\u043b\u043e",
                    ["Rule"] = "\u041f\u0440\u0430\u0432\u0438\u043b\u043e",
                    ["Active"] = "\u0410\u043a\u0442\u0438\u0432\u043d\u043e",
                    ["Stopped"] = "\u041e\u0441\u0442\u0430\u043d\u043e\u0432\u043b\u0435\u043d\u043e",
                    ["Open"] = "\u041e\u0442\u043a\u0440\u044b\u0442\u044c",
                    ["Hide"] = "\u0421\u043a\u0440\u044b\u0442\u044c",
                    ["StopProtection"] = "\u041e\u0441\u0442\u0430\u043d\u043e\u0432\u0438\u0442\u044c \u0437\u0430\u0449\u0438\u0442\u0443",
                    ["StartProtection"] = "\u0417\u0430\u043f\u0443\u0441\u0442\u0438\u0442\u044c \u0437\u0430\u0449\u0438\u0442\u0443",
                    ["Exit"] = "\u0412\u044b\u0445\u043e\u0434",
                    ["CloseTitle"] = "RDP Guard \u0437\u0430\u043a\u0440\u044b\u0432\u0430\u0435\u0442\u0441\u044f",
                    ["CloseMessage"] = "\u041f\u0440\u043e\u0433\u0440\u0430\u043c\u043c\u0430 \u043c\u043e\u0436\u0435\u0442 \u043f\u0440\u043e\u0434\u043e\u043b\u0436\u0438\u0442\u044c \u043f\u0440\u043e\u0432\u0435\u0440\u043a\u0443 \u0432 \u0444\u043e\u043d\u0435.",
                    ["MinimizeToTray"] = "\u0421\u0432\u0435\u0440\u043d\u0443\u0442\u044c \u0432 \u0442\u0440\u0435\u0439",
                    ["CloseProgram"] = "\u0417\u0430\u043a\u0440\u044b\u0442\u044c",
                    ["SettingsSaved"] = "\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u044b.",
                    ["LastCheck"] = "\u041f\u043e\u0441\u043b. \u043f\u0440\u043e\u0432\u0435\u0440\u043a\u0430: {0} \u0441\u043e\u0431., {1} IP, {2} \u043d\u043e\u0432. \u0431\u043b\u043e\u043a."
                },
                ["fr"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Threshold"] = "Seuil (>=)",
                    ["Interval"] = "Intervalle (min)",
                    ["Language"] = "Langue",
                    ["StartWithWindows"] = "D\u00e9marrer avec Windows",
                    ["Start"] = "D\u00e9marrer",
                    ["Stop"] = "Arr\u00eater",
                    ["CheckNow"] = "V\u00e9rifier",
                    ["Save"] = "Enregistrer",
                    ["Unblock"] = "Supprimer le blocage",
                    ["Whitelist"] = "Liste blanche",
                    ["BlockedIps"] = "IP bloqu\u00e9es",
                    ["Log"] = "Journal",
                    ["Ip"] = "IP bloqu\u00e9e",
                    ["Date"] = "Date",
                    ["Count"] = "Nombre",
                    ["Rule"] = "R\u00e8gle",
                    ["Active"] = "Actif",
                    ["Stopped"] = "Arr\u00eat\u00e9",
                    ["Open"] = "Ouvrir",
                    ["Hide"] = "Masquer",
                    ["StopProtection"] = "Arr\u00eater la protection",
                    ["StartProtection"] = "D\u00e9marrer la protection",
                    ["Exit"] = "Quitter",
                    ["CloseTitle"] = "Fermeture de RDP Guard",
                    ["CloseMessage"] = "Le programme peut continuer \u00e0 v\u00e9rifier les \u00e9checs d'audit en arri\u00e8re-plan.",
                    ["MinimizeToTray"] = "R\u00e9duire dans la zone de notification",
                    ["CloseProgram"] = "Fermer le programme",
                    ["SettingsSaved"] = "Param\u00e8tres enregistr\u00e9s.",
                    ["LastCheck"] = "Derni\u00e8re v\u00e9rification : {0} \u00e9v\u00e9nements, {1} IP, {2} nouveaux blocages"
                },
                ["es"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Threshold"] = "Umbral (>=)",
                    ["Interval"] = "Intervalo (min)",
                    ["Language"] = "Idioma",
                    ["StartWithWindows"] = "Iniciar con Windows",
                    ["Start"] = "Iniciar",
                    ["Stop"] = "Detener",
                    ["CheckNow"] = "Comprobar",
                    ["Save"] = "Guardar",
                    ["Unblock"] = "Quitar bloqueo",
                    ["Whitelist"] = "Lista blanca",
                    ["BlockedIps"] = "IP bloqueadas",
                    ["Log"] = "Registro",
                    ["Ip"] = "IP bloqueada",
                    ["Date"] = "Fecha",
                    ["Count"] = "Cantidad",
                    ["Rule"] = "Regla",
                    ["Active"] = "Activo",
                    ["Stopped"] = "Detenido",
                    ["Open"] = "Abrir",
                    ["Hide"] = "Ocultar",
                    ["StopProtection"] = "Detener protecci\u00f3n",
                    ["StartProtection"] = "Iniciar protecci\u00f3n",
                    ["Exit"] = "Salir",
                    ["CloseTitle"] = "RDP Guard se est\u00e1 cerrando",
                    ["CloseMessage"] = "El programa puede seguir comprobando errores de auditor\u00eda en segundo plano.",
                    ["MinimizeToTray"] = "Minimizar a la bandeja",
                    ["CloseProgram"] = "Cerrar programa",
                    ["SettingsSaved"] = "Configuraci\u00f3n guardada.",
                    ["LastCheck"] = "\u00daltima comprobaci\u00f3n: {0} eventos, {1} IP, {2} nuevos bloqueos"
                },
                ["zh"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Threshold"] = "\u9608\u503c (>=)",
                    ["Interval"] = "\u95f4\u9694\uff08\u5206\u949f\uff09",
                    ["Language"] = "\u8bed\u8a00",
                    ["StartWithWindows"] = "\u968f Windows \u542f\u52a8",
                    ["Start"] = "\u542f\u52a8",
                    ["Stop"] = "\u505c\u6b62",
                    ["CheckNow"] = "\u7acb\u5373\u68c0\u67e5",
                    ["Save"] = "\u4fdd\u5b58",
                    ["Unblock"] = "\u79fb\u9664\u963b\u6b62",
                    ["Whitelist"] = "\u767d\u540d\u5355",
                    ["BlockedIps"] = "\u5df2\u963b\u6b62 IP",
                    ["Log"] = "\u65e5\u5fd7",
                    ["Ip"] = "\u5df2\u963b\u6b62 IP",
                    ["Date"] = "\u65e5\u671f",
                    ["Count"] = "\u6b21\u6570",
                    ["Rule"] = "\u89c4\u5219",
                    ["Active"] = "\u6d3b\u52a8",
                    ["Stopped"] = "\u5df2\u505c\u6b62",
                    ["Open"] = "\u6253\u5f00",
                    ["Hide"] = "\u9690\u85cf",
                    ["StopProtection"] = "\u505c\u6b62\u4fdd\u62a4",
                    ["StartProtection"] = "\u542f\u52a8\u4fdd\u62a4",
                    ["Exit"] = "\u9000\u51fa",
                    ["CloseTitle"] = "RDP Guard \u6b63\u5728\u5173\u95ed",
                    ["CloseMessage"] = "\u7a0b\u5e8f\u53ef\u4ee5\u5728\u540e\u53f0\u7ee7\u7eed\u68c0\u67e5\u5ba1\u8ba1\u5931\u8d25\u3002",
                    ["MinimizeToTray"] = "\u6700\u5c0f\u5316\u5230\u6258\u76d8",
                    ["CloseProgram"] = "\u5173\u95ed\u7a0b\u5e8f",
                    ["SettingsSaved"] = "\u8bbe\u7f6e\u5df2\u4fdd\u5b58\u3002",
                    ["LastCheck"] = "\u4e0a\u6b21\u68c0\u67e5\uff1a{0} \u4e2a\u4e8b\u4ef6\uff0c{1} \u4e2a IP\uff0c{2} \u4e2a\u65b0\u963b\u6b62"
                }
            };

        public static IReadOnlyList<LanguageOption> Languages => LanguageList;

        public static string NormalizeLanguageCode(string code)
        {
            return LanguageList.Any(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))
                ? code.ToLowerInvariant()
                : "en";
        }

        public static string Text(string code, string key)
        {
            code = NormalizeLanguageCode(code);
            if (Texts.TryGetValue(code, out var table) && table.TryGetValue(key, out var value))
            {
                return value;
            }

            if (Texts["en"].TryGetValue(key, out var fallback))
            {
                return fallback;
            }

            return key;
        }

        public static string Format(string code, string key, params object[] args)
        {
            try
            {
                return string.Format(Text(code, key), args);
            }
            catch
            {
                try
                {
                    return string.Format(Text("en", key), args);
                }
                catch
                {
                    return key;
                }
            }
        }
    }
}
