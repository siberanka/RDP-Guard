# RDP Guard

RDP Guard monitors failed RDP sign-in attempts in the Windows Security log and blocks abusive remote IP addresses through Windows Firewall.

## Defaults

- Event ID: `4625`
- LogonType: `3` or `10`
- Threshold: `3` or more audit failures
- Check interval: `15` minutes
- Firewall rule name: `RDP_GUARD_yyyyMMdd_HHmmss_fff`
- If a check finds IPs to block, all of them are written into a single inbound firewall rule using the rule `remoteip` scope.
- If an IP is already covered by an active broad inbound block rule, it is not added to a new `RDP_GUARD_...` rule.
- Whitelist defaults: `127.0.0.1`, `::1`
- The app starts minimized to the system tray. Left-click opens the window; right-click shows open/check/start-stop/exit actions.
- Default language: `English`. Selected language is saved in config. Supported languages: English, Turkish, German, Russian, French, Spanish, Chinese.

## Build

```powershell
.\build.ps1
```

Output:

```text
bin\Release\RDPGuard.exe
```

The executable requires administrator privileges via manifest. The "start with Windows" option creates a Task Scheduler entry named `RDP Guard` with `RunLevel=Highest`.

RDP Guard does not analyze or block successful RDP logons. It only counts Security log `4625` failure events that contain a remote IP and match RDP/NLA-related logon types.

When closing the window, the app offers two choices: `Minimize to tray` or `Close program`.

## Stability Notes

- `netsh`, PowerShell, and `schtasks` calls use timeout handling and async output reading so a stuck system command does not freeze the app indefinitely.
- RDP Guard only updates or deletes its own safely named `RDP_GUARD_...` rules.
- If config state and actual firewall state diverge, the next check revalidates the record.
- Logs are written to `C:\ProgramData\RDPGuard\rdpguard.log` and rotated above 1 MB.
- Language selection only affects UI text. Event scanning, IP handling, firewall logic, counters, and time intervals are language-independent.
- On startup, RDP Guard attempts to raise its process priority to `High`, logs unhandled exceptions, and registers with Windows application restart for better background resilience.

---

# RDP Guard

RDP Guard, Windows Security log icindeki basarisiz RDP oturum acma denemelerini izler ve kotuye kullanim yapan uzak IP adreslerini Windows Firewall uzerinden engeller.

## Varsayilanlar

- Event ID: `4625`
- LogonType: `3` veya `10`
- Esik: `3` veya daha fazla audit failure
- Kontrol araligi: `15` dakika
- Firewall kural adi: `RDP_GUARD_yyyyMMdd_HHmmss_fff`
- Her kontrol turunda engellenecek IP varsa hepsi tek inbound firewall kuralina yazilir; IP'ler kuralin `remoteip` scope listesine eklenir.
- IP zaten aktif ve genel kapsamli bir inbound block firewall kuralinin remote scope'u tarafindan kapsaniyorsa yeni `RDP_GUARD_...` kuralina eklenmez.
- Whitelist varsayilanlari: `127.0.0.1`, `::1`
- Uygulama ilk acilista sistem tepsisine/gizli simgelere kucultulmus baslar. Sol tik pencereyi acar; sag tik ac/kontrol et/baslat-durdur/kapat seceneklerini gosterir.
- Varsayilan dil: `English`. Secilen dil config icinde saklanir. Desteklenen diller: Ingilizce, Turkce, Almanca, Rusca, Fransizca, Ispanyolca, Cince.

## Build

```powershell
.\build.ps1
```

Cikti:

```text
bin\Release\RDPGuard.exe
```

Exe manifest ile yonetici yetkisi ister. "Windows ile baslat" secenegi, Task Scheduler icinde `RDP Guard` adli gorevi `RunLevel=Highest` olarak olusturur.

RDP Guard basarili RDP girislerini analiz etmez veya bloklamaz. Yalnizca Security log altindaki, uzak IP alanina sahip ve RDP/NLA ile ilgili logon tiplerine uyan `4625` basarisiz oturum acma olaylarini sayar.

Pencere kapatilirken iki secenek sunulur: `Simge durumuna kucult` veya `Programi kapat`.

## Kararlilik Notlari

- `netsh`, PowerShell ve `schtasks` cagrilari timeout ve async output okuma ile calisir; takilan sistem komutu uygulamayi sonsuza kadar dondurmez.
- RDP Guard yalnizca guvenli `RDP_GUARD_...` ad formatindaki kendi kurallarini gunceller veya siler.
- Config ile gercek firewall durumu ayrisirsa bir sonraki kontrolde kayit yeniden dogrulanir.
- Loglar `C:\ProgramData\RDPGuard\rdpguard.log` altinda tutulur ve 1 MB uzerinde rotate edilir.
- Dil secimi yalnizca UI/metin katmanini etkiler. Event tarama, IP islemleri, firewall mantigi, sayaclar ve zaman araliklari dil ayarindan bagimsizdir.
- RDP Guard acilista islem onceligini `High` yapmayi dener, yakalanmamis hatalari loglar ve arka plan kararliligi icin Windows application restart kaydi olusturur.
