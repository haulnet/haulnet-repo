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
    public static string UnreachableBody(string host) => T(
        $"Could not reach {Name(host, "the host")}'s PC directly. Join the same local network or VPN (Radmin, WireGuard, Tailscale), or ask the host to allow R.E.P.O. and luxon_server in Windows Firewall, or to create the game in Photon Cloud.",
        $"Не удалось достучаться до ПК {Name(host, "хоста")} напрямую. Подключитесь к одной локальной сети или VPN (Radmin, WireGuard, Tailscale), либо попроси хоста разрешить R.E.P.O. и luxon_server в брандмауэре Windows или создать игру в Photon Cloud.");

    // Host: before hosting without any address reachable from outside
    public static string LanOnlyHeader => T("ONLY YOUR HOME NETWORK?", "ТОЛЬКО ДОМАШНЯЯ СЕТЬ?");
    public static string LanOnlyBody => T(
        "Your PC has no VPN (Radmin, WireGuard, Tailscale) and no public address. Friends on your home network can join; friends elsewhere probably cannot. For them, create the game in Photon Cloud.",
        "У твоего ПК нет VPN (Radmin, WireGuard, Tailscale) и внешнего адреса. Друзья из твоей домашней сети подключатся, остальные, скорее всего, нет. Для них создай игру в Photon Cloud.");

    // Host: a friend did not make it in
    public static string FriendFailedHeader => T("HAULNET: FRIEND NOT CONNECTED", "HAULNET: ДРУГ НЕ ПОДКЛЮЧИЛСЯ");
    public static string FriendUnreachableBody(string friend) => T(
        $"{Name(friend, "A friend")} could not reach your PC. Join the same VPN (Radmin, WireGuard, Tailscale), allow R.E.P.O. and luxon_server in Windows Firewall, or create the game in Photon Cloud.",
        $"{Name(friend, "Друг")} не смог достучаться до твоего ПК. Подключитесь к одному VPN (Radmin, WireGuard, Tailscale), разреши R.E.P.O. и luxon_server в брандмауэре Windows или создай игру в Photon Cloud.");
    public static string FriendLeftBody(string friend) => T(
        $"{Name(friend, "A friend")} accepted the invite but left before joining. They may not have Haulnet installed, or cannot reach your PC.",
        $"{Name(friend, "Друг")} принял приглашение, но вышел, не дойдя до игры. Возможно, у него нет Haulnet или он не может достучаться до твоего ПК.");
    public static string FriendSlowBody(string friend, int seconds) => T(
        $"{Name(friend, "A friend")} still has not joined after {seconds} seconds. They may not be able to reach your PC.",
        $"{Name(friend, "Друг")} всё ещё не подключился за {seconds} секунд. Возможно, он не может достучаться до твоего ПК.");

    private static string Name(string name, string fallback) => string.IsNullOrWhiteSpace(name) ? fallback : name;
    public static string Ok => "OK";
    public static string WrongPasswordHeader => T("WRONG PASSWORD", "НЕВЕРНЫЙ ПАРОЛЬ");
    public static string WrongPasswordBody => T(
        "The lobby password you entered is not correct.",
        "Пароль лобби введён неверно.");
    public static string BannedHeader => T("BANNED", "ВЫ ЗАБАНЕНЫ");
    public static string BannedBody => T(
        "The host has banned you from this game.",
        "Хост запретил тебе заходить в эту игру.");

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
