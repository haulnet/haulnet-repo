namespace Haulnet;

/// <summary>
/// Player-facing strings. English is the source language; translations will hook in here.
/// </summary>
internal static class Text
{
    public const string Title = "Haulnet";
    public const string ToggleHint = "F8";

    public const string ModeCloud = "Cloud";
    public const string ModeHost = "Host";
    public const string ModeJoin = "Join";

    public const string CloudInfo = "Normal online play through Photon Cloud. Nothing is changed.";

    public const string HostInfo = "Your game runs the server. Start a Private game as usual, then invite friends.";
    public const string ServerStopped = "Server: stopped";
    public const string ServerStarting = "Server: starting...";
    public const string ServerRunning = "Server: running";
    public const string ServerFailed = "Server error:";
    public const string Retry = "Retry";
    public const string FriendsUse = "Friends select Join and use one of these addresses:";
    public const string NoAddresses = "No network addresses found. Are you connected to a network?";
    public const string Copy = "Copy";
    public const string Copied = "Copied";
    public const string FirewallNote = "If Windows asks about the firewall for luxon_server, allow it, or friends cannot connect.";

    public const string JoinInfo = "Enter the host's address, then join through a Steam invite or \"Join friend\".";
    public const string AddressLabel = "Host address:";
    public const string AddressInvalid = "Invalid address:";
    public const string LanGames = "Games on your local network:";
    public const string LanNone = "None found yet. Host must be in Host mode on the same network.";
    public const string Use = "Use";
    public const string Incompatible = "(different Haulnet version)";

    public const string LockedInRoom = "Leave the lobby to change the mode.";
}
