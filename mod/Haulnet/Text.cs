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

    // Region page ("where to play")
    public static string SectionHaulnet => "HAULNET";
    public static string SectionPhoton => "PHOTON CLOUD";
    public static string RowOnMyPc => T("On my PC", "На моём ПК");
    public static string RowByAddress => T("+ Server by address...", "+ Сервер по адресу...");
    public static string HintFull => T("full", "заполнен");
    public static string Checking => T("checking...", "проверка...");
    public static string NoPing => "—";
    public static string Ms => T("ms", "мс");

    // Server by address
    public static string AddressHeader => T("SERVER BY ADDRESS", "СЕРВЕР ПО АДРЕСУ");
    public static string AddressBody => T("SERVER ADDRESS", "АДРЕС СЕРВЕРА");
    public static string Connect => T("CONNECT", "ПОДКЛЮЧИТЬСЯ");
    public static string AddressInvalidHeader => T("WRONG ADDRESS", "НЕВЕРНЫЙ АДРЕС");
    public static string AddressInvalidBody(string text, string error) => T(
        $"\"{text}\" is not a server address ({error}).\nUse host or host:port, for example play.example.org or 203.0.113.5:27100.",
        $"«{text}» не похоже на адрес сервера ({error}).\nНужно имя или имя:порт, например play.example.org или 203.0.113.5:27100.");
    public static string NotFoundHeader => T("SERVER NOT FOUND", "СЕРВЕР НЕ НАЙДЕН");
    public static string NotFoundBody(string host) => T(
        $"The name {host} does not resolve to an address.",
        $"Имя {host} не находится в DNS.");
    public static string FullHeader => T("SERVER IS FULL", "СЕРВЕР ЗАПОЛНЕН");
    public static string FullBody(string server) => T(
        $"{server} does not accept new games (its status says accepting: false).",
        $"{server} не принимает новые игры (статус сервера: accepting: false).");

    // Connection failures
    public static string NoAnswerHeader => T("HAULNET: NO CONNECTION", "HAULNET: НЕТ ПОДКЛЮЧЕНИЯ");
    public static string NoAnswerBody(string server, int port) => T(
        $"No answer from {server} on UDP {port}.",
        $"Нет ответа от {server} по UDP {port}.");

    // On my PC
    public static string ServerFailedHeader => T("HAULNET: SERVER ERROR", "HAULNET: ОШИБКА СЕРВЕРА");
    public static string ServerFailedBody(string error) => T(
        $"The server on your PC could not start.\n\n{error}",
        $"Не удалось запустить сервер на твоём ПК.\n\n{error}");
    public static string PortHeader => T("PORT", "ПОРТ");
    public static string PortBody => T("UDP PORT", "UDP-ПОРТ");
    public static string Create => T("CREATE", "СОЗДАТЬ");
    public static string PortInvalidHeader => T("WRONG PORT", "НЕВЕРНЫЙ ПОРТ");
    public static string PortInvalidBody(string text) => T(
        $"\"{text}\": the port must be a number from 1024 to 65533. The game uses it and the next two ports.",
        $"«{text}»: порт должен быть числом от 1024 до 65533. Игра занимает его и два следующих.");

    // Friend: the host's PC could not be reached
    public static string UnreachableBody(string host, string addresses) => T(
        $"No answer from {Name(host, "the host")}'s PC ({addresses}).",
        $"Нет ответа от ПК {Name(host, "хоста")} ({addresses}).");

    // Host: a friend reported that the connection failed
    public static string FriendFailedHeader => T("HAULNET: FRIEND NOT CONNECTED", "HAULNET: ДРУГ НЕ ПОДКЛЮЧИЛСЯ");
    public static string FriendUnreachableBody(string friend, int port) => T(
        $"{Name(friend, "A friend")} got no answer from your PC on UDP {port}-{port + 2}.",
        $"{Name(friend, "Друг")} не получил ответа от твоего ПК по UDP {port}-{port + 2}.");

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
    public static string IncompatibleHeader => T("HAULNET IS OFF", "HAULNET ВЫКЛЮЧЕН");
    public static string IncompatibleBody => T(
        "Haulnet is not compatible with this game version yet. You are playing through Photon Cloud as usual.",
        "Haulnet пока не совместим с этой версией игры. Ты играешь через Photon Cloud, как обычно.");

    // Lobby status line
    public static string StatusHosting(int port) => T(
        $"Haulnet · on your PC · UDP {port}-{port + 2}",
        $"Haulnet · на твоём ПК · UDP {port}-{port + 2}");
    public static string StatusServer(string server) => T($"Haulnet · server {server}", $"Haulnet · сервер {server}");
    public static string StatusFriendPc(string address) => T($"Haulnet · host {address}", $"Haulnet · хост {address}");
}
