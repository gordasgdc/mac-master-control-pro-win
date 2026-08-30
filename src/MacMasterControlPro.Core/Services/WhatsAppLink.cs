using System;

namespace MacMasterControlPro.Core.Services;

/// Port 1:1 al WhatsAppLink.swift (Mac) — numarul reconstruit din bucati.
public static class WhatsAppLink
{
    private static readonly string[] Parts = { "34", "643", "109", "970" };
    private static string Number => string.Join("", Parts);

    public static string Url(string? text = null)
    {
        var baseUrl = $"https://wa.me/{Number}";
        return text is null ? baseUrl : $"{baseUrl}?text={Uri.EscapeDataString(text)}";
    }
}
