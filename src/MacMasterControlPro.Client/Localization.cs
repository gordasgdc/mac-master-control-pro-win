using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MacMasterControlPro.Client;

public enum AppLanguage { Ro, En, Es }

public static class AppLanguageExtensions
{
    public static string Label(this AppLanguage lang) => lang switch
    {
        AppLanguage.Ro => "Română",
        AppLanguage.En => "English",
        AppLanguage.Es => "Español",
        _ => "Română",
    };
}

/// Port 1:1 al Localization.swift (Mac) — dictionar minimal, acopera
/// chrome-ul principal (sidebar, dashboard, setari, poarta de trial).
public static class LanguageStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Master Control Studio Pro", "language.txt");

    public static event Action? Changed;

    private static AppLanguage _current = Load();
    public static AppLanguage Current
    {
        get => _current;
        set
        {
            _current = value;
            Save(value);
            Changed?.Invoke();
        }
    }

    private static AppLanguage Load()
    {
        try
        {
            if (File.Exists(FilePath) && Enum.TryParse<AppLanguage>(File.ReadAllText(FilePath).Trim(), out var saved))
            {
                return saved;
            }
        }
        catch { /* fallback la detectie sistem */ }

        var iso = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return iso switch
        {
            "es" => AppLanguage.Es,
            "en" => AppLanguage.En,
            _ => AppLanguage.Ro,
        };
    }

    private static void Save(AppLanguage lang)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, lang.ToString());
        }
        catch { /* nescriere nu blocheaza UI */ }
    }
}

public static class L
{
    private static readonly Dictionary<string, Dictionary<AppLanguage, string>> Strings = new()
    {
        ["sidebar.dashboard"] = new() { [AppLanguage.Ro] = "Dashboard", [AppLanguage.En] = "Dashboard", [AppLanguage.Es] = "Panel" },
        ["sidebar.network"] = new() { [AppLanguage.Ro] = "Rețea", [AppLanguage.En] = "Network", [AppLanguage.Es] = "Red" },
        ["sidebar.cloud"] = new() { [AppLanguage.Ro] = "Cloud Manager", [AppLanguage.En] = "Cloud Manager", [AppLanguage.Es] = "Gestor Cloud" },
        ["sidebar.cleanup"] = new() { [AppLanguage.Ro] = "Curățare & RAM", [AppLanguage.En] = "Cleanup & RAM", [AppLanguage.Es] = "Limpieza y RAM" },
        ["sidebar.tweaks"] = new() { [AppLanguage.Ro] = "Tweak-uri Sistem", [AppLanguage.En] = "System Tweaks", [AppLanguage.Es] = "Ajustes del Sistema" },
        ["sidebar.dependencies"] = new() { [AppLanguage.Ro] = "Dependențe", [AppLanguage.En] = "Dependencies", [AppLanguage.Es] = "Dependencias" },
        ["sidebar.settings"] = new() { [AppLanguage.Ro] = "Setări", [AppLanguage.En] = "Settings", [AppLanguage.Es] = "Ajustes" },
        ["sidebar.checkUpdates"] = new() { [AppLanguage.Ro] = "Caută actualizări", [AppLanguage.En] = "Check for updates", [AppLanguage.Es] = "Buscar actualizaciones" },

        ["dashboard.title"] = new() { [AppLanguage.Ro] = "📊 Dashboard", [AppLanguage.En] = "📊 Dashboard", [AppLanguage.Es] = "📊 Panel" },
        ["dashboard.tagline"] = new()
        {
            [AppLanguage.Ro] = "Ultimate System Tuning, Cloud Mount, Media Cache & Future macOS Readiness Panel",
            [AppLanguage.En] = "Ultimate System Tuning, Cloud Mount, Media Cache & Future macOS Readiness Panel",
            [AppLanguage.Es] = "Panel definitivo de optimización, montaje Cloud, caché multimedia y preparación para futuros sistemas",
        },

        ["settings.appearance"] = new() { [AppLanguage.Ro] = "Aspect", [AppLanguage.En] = "Appearance", [AppLanguage.Es] = "Apariencia" },
        ["settings.theme"] = new() { [AppLanguage.Ro] = "Temă", [AppLanguage.En] = "Theme", [AppLanguage.Es] = "Tema" },
        ["settings.textSize"] = new() { [AppLanguage.Ro] = "Mărime text", [AppLanguage.En] = "Text Size", [AppLanguage.Es] = "Tamaño de texto" },
        ["settings.language"] = new() { [AppLanguage.Ro] = "Limbă", [AppLanguage.En] = "Language", [AppLanguage.Es] = "Idioma" },
        ["settings.profile"] = new() { [AppLanguage.Ro] = "Profil", [AppLanguage.En] = "Profile", [AppLanguage.Es] = "Perfil" },
        ["settings.guide"] = new() { [AppLanguage.Ro] = "Deschide Ghidul de Utilizare (PDF)", [AppLanguage.En] = "Open User Guide (PDF)", [AppLanguage.Es] = "Abrir Guía de Usuario (PDF)" },
        ["settings.name"] = new() { [AppLanguage.Ro] = "Nume", [AppLanguage.En] = "Name", [AppLanguage.Es] = "Nombre" },
        ["settings.email"] = new() { [AppLanguage.Ro] = "Email", [AppLanguage.En] = "Email", [AppLanguage.Es] = "Correo" },

        ["trial.title"] = new() { [AppLanguage.Ro] = "Analiza este 100% completă", [AppLanguage.En] = "Analysis is 100% complete", [AppLanguage.Es] = "El análisis está 100% completo" },
        ["trial.body"] = new()
        {
            [AppLanguage.Ro] = "Susține dezvoltarea cu o donație (17€, o singură dată) pentru a debloca aplicarea modificărilor.",
            [AppLanguage.En] = "Support development with a one-time 17€ donation to unlock applying changes.",
            [AppLanguage.Es] = "Apoya el desarrollo con una donación única de 17€ para desbloquear la aplicación de cambios.",
        },
        ["trial.donate"] = new() { [AppLanguage.Ro] = "Donează din GDC Plugin Manager", [AppLanguage.En] = "Donate via GDC Plugin Manager", [AppLanguage.Es] = "Donar desde GDC Plugin Manager" },
        ["trial.activate"] = new() { [AppLanguage.Ro] = "Activează", [AppLanguage.En] = "Activate", [AppLanguage.Es] = "Activar" },
        ["trial.cancel"] = new() { [AppLanguage.Ro] = "Anulează", [AppLanguage.En] = "Cancel", [AppLanguage.Es] = "Cancelar" },
        ["trial.key"] = new() { [AppLanguage.Ro] = "Cheie de licență", [AppLanguage.En] = "License key", [AppLanguage.Es] = "Clave de licencia" },
    };

    public static string T(string key)
    {
        if (!Strings.TryGetValue(key, out var byLang)) return key;
        return byLang.TryGetValue(LanguageStore.Current, out var value) ? value : byLang[AppLanguage.Ro];
    }
}
