using System;
using UnityEngine.Localization.Settings;

namespace Haulnet;

/// <summary>
/// Player-facing strings. English is the source language; Russian follows the game's selected language.
/// </summary>
internal static class Text
{
    private static bool Ru
    {
        get
        {
            try
            {
                var code = LocalizationSettings.SelectedLocale?.Identifier.Code;
                return code != null && code.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    private static string T(string en, string ru) => Ru ? ru : en;

    public const string Title = "Haulnet";

    // Host choice popup
    public static string WhereHeader => T("WHERE TO HOST?", "ГДЕ СОЗДАТЬ ИГРУ?");
    public static string WhereBody => T(
        "On your PC: friends connect straight to you, no Photon servers in between. Everyone needs Haulnet.\n\nPhoton Cloud: as in the normal game, anyone can join.",
        "На твоём ПК: друзья подключаются прямо к тебе, без серверов Photon. У всех должен быть Haulnet.\n\nPhoton Cloud: как в обычной игре, зайти может кто угодно.");
    public static string OptionMyPc => T("ON MY PC", "НА МОЁМ ПК");
    public static string OptionCloud => "PHOTON CLOUD";

    // Popups
    public static string ServerFailedHeader => T("HAULNET: SERVER ERROR", "HAULNET: ОШИБКА СЕРВЕРА");
    public static string ServerFailedBody(string error) => T(
        $"The server on your PC could not start.\n\n{error}",
        $"Не удалось запустить сервер на твоём ПК.\n\n{error}");
    public static string UnreachableHeader => T("HAULNET: HOST NOT REACHABLE", "HAULNET: ХОСТ НЕДОСТУПЕН");
    public static string UnreachableBody => T(
        "Your game could not reach the host directly. Join the same local network or VPN as the host, or ask the host to allow Haulnet in Windows Firewall or forward UDP 27000-27002.",
        "Не удалось достучаться до хоста напрямую. Подключись к той же локальной сети или VPN, что и хост, либо попроси хоста разрешить Haulnet в брандмауэре Windows или пробросить UDP 27000-27002.");
    public static string Ok => "OK";

    // Lobby status line
    public static string StatusHosting => T("Haulnet · hosted on your PC", "Haulnet · игра на твоём ПК");
    public static string StatusJoined(string address, string label) => T(
        $"Haulnet · host {address} · {label}",
        $"Haulnet · хост {address} · {label}");
    public static string StatusManual(string address) => T($"Haulnet · server {address}", $"Haulnet · сервер {address}");

    // Address labels (written into Steam lobby data by the host and shown to friends)
    public static string LabelVpn(string adapter) => $"VPN ({adapter})";
    public static string LabelLan(string adapter) => T($"local network ({adapter})", $"локальная сеть ({adapter})");
    public static string LabelIPv6 => "IPv6";
    public static string LabelPublic => T("internet", "интернет");
    public static string LabelUnverified => T("not confirmed", "не подтверждено");

    // Diagnostics panel (F8)
    public static string PanelSession => T("Next connection:", "Следующее подключение:");
    public static string SessionCloud => T("Photon Cloud (normal game)", "Photon Cloud (обычная игра)");
    public static string SessionHost => T("your PC (Haulnet server)", "твой ПК (сервер Haulnet)");
    public static string SessionManual(string address) => T($"server {address}", $"сервер {address}");
    public static string ServerRunning => T("Server: running", "Сервер: работает");
    public static string ServerStarting => T("Server: starting...", "Сервер: запускается...");
    public static string ServerStopped => T("Server: stopped", "Сервер: остановлен");
    public static string ServerError => T("Server error:", "Ошибка сервера:");
    public static string Retry => T("Retry", "Повторить");
    public static string YourAddresses => T(
        "Your addresses (friends receive them with the Steam invite):",
        "Твои адреса (друзья получают их вместе со Steam-инвайтом):");
    public static string NoAddresses => T("No network addresses found.", "Сетевые адреса не найдены.");
    public static string Copy => T("Copy", "Копировать");
    public static string Copied => T("Copied", "Скопировано");
    public static string ManualJoin => T(
        "Server by address (applies to the next Public game):",
        "Сервер по адресу (для следующей публичной игры):");
    public static string Apply => T("Use", "Использовать");
    public static string Clear => T("Clear", "Сбросить");
    public static string AddressInvalid => T("Invalid address:", "Неверный адрес:");
    public static string FirewallNote => T(
        "If Windows asks about the firewall for luxon_server or R.E.P.O., allow it, or friends cannot connect.",
        "Если Windows спросит про брандмауэр для luxon_server или R.E.P.O., разреши доступ, иначе друзья не подключатся.");
}
