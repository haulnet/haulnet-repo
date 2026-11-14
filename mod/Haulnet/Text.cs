using System;
using System.Collections.Generic;
using UnityEngine.Localization.Settings;

namespace Haulnet;

/// <summary>
/// Player-facing strings in the game's selected language (ADR 0018). The texts live in <c>Localization/*.tsv</c>,
/// embedded in the DLL; English is the source and fills every gap. The language is read on every access, so a screen
/// built after a language change shows the new language.
/// </summary>
internal static class Text
{
    private static readonly Translations Strings = new(name =>
        Translations.ReadResource(typeof(Text).Assembly, $"Haulnet.Localization.{name}.tsv"));

    private static readonly HashSet<string> Reported = new();

    public const string Title = "Haulnet";

    private static string? Locale
    {
        get
        {
            try
            {
                return LocalizationSettings.SelectedLocale?.Identifier.Code;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    private static string Get(string key, params (string Name, object? Value)[] args)
    {
        var text = Strings.Get(Locale, key);
        if (Strings.LoadErrors.Count > 0)
        {
            foreach (var error in Strings.LoadErrors)
            {
                Plugin.Log.LogWarning($"Translations: {error}");
            }

            Strings.LoadErrors.Clear();
        }

        if (text == null)
        {
            if (Reported.Add(key))
            {
                Plugin.Log.LogError($"Translations: no text for {key}.");
            }

            return key;
        }

        return Translations.Format(text, args);
    }

    // Names, not translated.
    public static string SectionHaulnet => "HAULNET";
    public static string SectionPhoton => "PHOTON CLOUD";
    public static string NoPing => "—";

    // Region page ("where to play")
    public static string RowOnMyPc => Get("ROW.ON_MY_PC");
    public static string RowByAddress => Get("ROW.BY_ADDRESS");
    public static string HintFull => Get("ROW.FULL");
    public static string Checking => Get("ROW.CHECKING");
    public static string Ping(string ms) => Get("ROW.PING", ("ping", ms));
    public static string RowThisPc(string name) => Get("ROW.THIS_PC", ("name", name.Length > 0 ? name : "haulnet-server"));
    public static string NoServerHeader => Get("NO_SERVER.HEADER");
    public static string NoServerBody(string city) => Get("NO_SERVER.BODY", ("city", city));

    // Server by address
    public static string AddressHeader => Get("ADDRESS.HEADER");
    public static string AddressBody => Get("ADDRESS.TITLE");
    public static string Connect => Get("ADDRESS.CONNECT");
    // Fallback only: the text popups normally use the game's own localized "Back" (WherePage.LocalizePopupBack).
    public static string Back => Get("BACK");
    public static string AddressInvalidHeader => Get("ADDRESS_INVALID.HEADER");
    public static string AddressInvalidBody(string text, AddressError error) =>
        Get("ADDRESS_INVALID.BODY", ("text", text), ("reason", AddressReason(error)));
    public static string NotFoundHeader => Get("NOT_FOUND.HEADER");
    public static string NotFoundBody(string host) => Get("NOT_FOUND.BODY", ("host", host));
    public static string FullHeader => Get("FULL.HEADER");
    public static string FullBody(string server) => Get("FULL.BODY", ("server", server));

    private static string AddressReason(AddressError error) => error.Problem switch
    {
        AddressProblem.Empty => Get("ADDRESS_ERROR.EMPTY"),
        AddressProblem.MissingBracket => Get("ADDRESS_ERROR.MISSING_BRACKET"),
        AddressProblem.AfterBracket => Get("ADDRESS_ERROR.AFTER_BRACKET", ("value", error.Value)),
        AddressProblem.InvalidHost => Get("ADDRESS_ERROR.INVALID_HOST", ("value", error.Value)),
        AddressProblem.InvalidPort => Get("ADDRESS_ERROR.INVALID_PORT", ("value", error.Value)),
        AddressProblem.InvalidIPv6 => Get("ADDRESS_ERROR.INVALID_IPV6", ("value", error.Value)),
        _ => error.ToString(),
    };

    // Connection failures
    public static string NoAnswerHeader => Get("NO_ANSWER.HEADER");
    public static string NoAnswerBody(string server, int port) => Get("NO_ANSWER.BODY", ("server", server), ("port", port));

    // On my PC
    public static string ServerFailedHeader => Get("SERVER_FAILED.HEADER");
    public static string ServerFailedBody(ServerError error) => Get("SERVER_FAILED.BODY", ("reason", ServerReason(error)));
    public static string PortHeader => Get("PORT.HEADER");
    public static string PortBody => Get("PORT.TITLE");
    public static string Create => Get("PORT.CREATE");
    public static string PortInvalidHeader => Get("PORT_INVALID.HEADER");
    public static string PortInvalidBody(string text) => Get("PORT_INVALID.BODY", ("text", text));

    private static string ServerReason(ServerError error) => error.Problem switch
    {
        ServerProblem.FileMissing => Get("SERVER_ERROR.FILE_MISSING", ("path", error.Detail)),
        ServerProblem.PortInUse => Get("SERVER_ERROR.PORT_IN_USE", ("port", error.Number)),
        ServerProblem.StartFailed => Get("SERVER_ERROR.START_FAILED", ("reason", error.Detail)),
        ServerProblem.Exited => Get("SERVER_ERROR.EXITED", ("code", error.Number), ("output", error.Detail)),
        _ => error.ToString(),
    };

    // Friend: the host's PC could not be reached
    public static string UnreachableBody(string host, string addresses) => string.IsNullOrWhiteSpace(host)
        ? Get("UNREACHABLE.BODY_UNNAMED", ("addresses", addresses))
        : Get("UNREACHABLE.BODY", ("host", host), ("addresses", addresses));

    // Host: a friend reported that the connection failed
    public static string FriendFailedHeader => Get("FRIEND_FAILED.HEADER");
    public static string FriendUnreachableBody(string friend, int port) => string.IsNullOrWhiteSpace(friend)
        ? Get("FRIEND_UNREACHABLE.BODY_UNNAMED", ("first", port), ("last", port + 2))
        : Get("FRIEND_UNREACHABLE.BODY", ("friend", friend), ("first", port), ("last", port + 2));

    public static string Ok => Get("OK");
    public static string WrongPasswordHeader => Get("WRONG_PASSWORD.HEADER");
    public static string WrongPasswordBody => Get("WRONG_PASSWORD.BODY");
    public static string BannedHeader => Get("BANNED.HEADER");
    public static string BannedBody => Get("BANNED.BODY");
    public static string IncompatibleHeader => Get("INCOMPATIBLE.HEADER");
    public static string IncompatibleBody => Get("INCOMPATIBLE.BODY");

    // Lobby status line
    public static string StatusHosting(int port) => Get("STATUS.HOSTING", ("first", port), ("last", port + 2));
    public static string StatusServer(string server) => Get("STATUS.SERVER", ("server", server));
    public static string StatusFriendPc(string address) => Get("STATUS.FRIEND_PC", ("address", address));
}
