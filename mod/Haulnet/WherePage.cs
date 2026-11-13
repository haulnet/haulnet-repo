using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Haulnet;

/// <summary>
/// The game's region page becomes "where to play" (ADR 0015): above the Photon regions the mod adds a Haulnet
/// section with "On my PC" (private games only), saved and recent servers, and "Server by address". Choosing a
/// row sets the target for this game and continues the game's own flow exactly like a region would (saves page
/// for a private game, the Public game page for a public one).
/// </summary>
/// <remarks>
/// Hooks (game code), all optional; without them the page is the unmodded one and the game plays through Photon:
/// <list type="bullet">
/// <item><c>MenuPageRegions.Start</c> prefix: clear the previous choice before the page connects to Photon;
/// postfix: add the rows. The page instantiates its own rows only after Photon answers, from
/// <c>transformStartPosition</c> downwards, 32 units apart; the Haulnet rows take the top and move that start
/// position down, so they work even when Photon Cloud is unreachable.</item>
/// <item><c>MenuPageRegions.PickRegion</c> prefix: rows carry codes <c>haulnet:*</c>, handled here.</item>
/// <item><c>MenuTextInput.Update</c> postfix: Ctrl+V in the address popup (the game's text field has no paste).</item>
/// </list>
/// </remarks>
internal static class WherePage
{
    private const string CodePrefix = "haulnet:";
    private const string CodeMyPc = "haulnet:mypc";
    private const string CodeAddress = "haulnet:address";
    private const string CodeServer = "haulnet:server:";
    private const float RowStep = 32f;
    private const float CheckTimeoutSeconds = 6f;

    private static readonly Color PingGood = new(0.2f, 0.8f, 0.2f);
    private static readonly Color PingFair = new(0.8f, 0.8f, 0.2f);
    private static readonly Color PingPoor = new(0.8f, 0.4f, 0.2f);
    private static readonly Color PingBad = new(0.8f, 0.2f, 0.2f);
    private static readonly Color Grey = new(0.6f, 0.6f, 0.6f);

    // Game internals: MenuPageRegions.type (internal enum), MenuElementRegion.regionCode, DataDirector.networkRegion,
    // MenuTextInput.textCurrent / InputTextSet.
    private static readonly FieldInfo? PageTypeField = AccessTools.Field(typeof(MenuPageRegions), "type");
    private static readonly AccessTools.FieldRef<MenuElementRegion, string> RegionCode =
        AccessTools.FieldRefAccess<MenuElementRegion, string>("regionCode");
    private static readonly AccessTools.FieldRef<MenuTextInput, string> TextCurrent =
        AccessTools.FieldRefAccess<MenuTextInput, string>("textCurrent");
    private static readonly MethodInfo? InputTextSet = AccessTools.Method(typeof(MenuTextInput), "InputTextSet");

    private static readonly List<ServerRow> Rows = new();
    private static MenuElementRegion? _addressRow;
    private static MenuTextInput? _addressInput;
    private static string _draft = "";
    private static bool _continuing;
    private static bool _busy;

    /// <summary>False when the game's region page changed and Haulnet cannot offer its places to play.</summary>
    public static bool Available { get; private set; }

    public static void Apply(Harmony harmony)
    {
        var start = AccessTools.Method(typeof(MenuPageRegions), "Start");
        var pick = AccessTools.Method(typeof(MenuPageRegions), nameof(MenuPageRegions.PickRegion));
        if (start == null || pick == null || PageTypeField == null)
        {
            Plugin.Log.LogError("MenuPageRegions.Start/PickRegion/type not found (game update?); Haulnet cannot offer servers, the game plays through Photon Cloud.");
            return;
        }

        harmony.Patch(start,
            prefix: new HarmonyMethod(typeof(WherePage), nameof(BeforeRegionsStart)),
            postfix: new HarmonyMethod(typeof(WherePage), nameof(AfterRegionsStart)));
        harmony.Patch(pick, prefix: new HarmonyMethod(typeof(WherePage), nameof(BeforePickRegion)));
        Available = true;

        var update = AccessTools.Method(typeof(MenuTextInput), "Update");
        if (update != null && InputTextSet != null)
        {
            harmony.Patch(update, postfix: new HarmonyMethod(typeof(WherePage), nameof(AfterTextInputUpdate)));
        }
        else
        {
            Plugin.Log.LogWarning("MenuTextInput.Update not found; Ctrl+V in the address popup is off.");
        }
    }

    // ---------- Building the page ----------

    private static void BeforeRegionsStart()
    {
        // Back on the region page: whatever was chosen before no longer applies. Runs before the page's own
        // Photon connect, which must go to Photon Cloud to list the real regions.
        Session.ChooseCloud();
    }

    private static void AfterRegionsStart(MenuPageRegions __instance)
    {
        try
        {
            Build(__instance);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Could not add Haulnet rows to the region page: {e}");
        }
    }

    private static bool IsHostFlow(MenuPageRegions page) => Convert.ToInt32(PageTypeField!.GetValue(page)) == 0;

    private static void Build(MenuPageRegions page)
    {
        Rows.Clear();
        _addressRow = null;
        _busy = false;

        var start = page.transformStartPosition;
        var parent = start.parent;
        var originalStart = start.position;
        var pos = originalStart;

        var haulnetLabelPos = pos;
        pos.y -= RowStep;

        var rows = new List<MenuElementRegion>();
        if (IsHostFlow(page))
        {
            var port = Plugin.Settings.HostPort;
            rows.Add(AddRow(page, parent, ref pos, CodeMyPc, Text.RowOnMyPc, $"UDP {port}", Grey));
        }

        foreach (var server in KnownServers.All())
        {
            var element = AddRow(page, parent, ref pos, CodeServer + Rows.Count, server.Display, Text.Checking, Grey);
            Rows.Add(new ServerRow(server, element));
            rows.Add(element);
        }

        _addressRow = AddRow(page, parent, ref pos, CodeAddress, Text.RowByAddress, "", Grey);
        rows.Add(_addressRow);

        var template = rows[0].textName;
        AddLabel(template, parent, rows[0].transform.position, haulnetLabelPos, Text.SectionHaulnet);
        AddLabel(template, parent, rows[0].transform.position, pos, Text.SectionPhoton);
        pos.y -= RowStep;

        // The game's own rows start below ours; its loading animation moves with them.
        var shift = originalStart.y - pos.y;
        start.position = pos;
        if (page.loadingGraphics != null)
        {
            page.loadingGraphics.transform.position -= new Vector3(0f, shift, 0f);
        }

        // The page reveals its list only after Photon answered; ours must be usable before that.
        if (page.scrollCanvasGroup != null)
        {
            page.scrollCanvasGroup.alpha = 1f;
        }

        Plugin.Runner.StartCoroutine(UpdateServerRows(Rows.ToList()));
        Plugin.Log.LogInfo($"Region page: {rows.Count} Haulnet row(s) added ({Rows.Count} known server(s)).");
    }

    private static MenuElementRegion AddRow(MenuPageRegions page, Transform parent, ref Vector3 pos, string code, string name, string hint, Color hintColor)
    {
        var element = Object.Instantiate(page.regionPrefab, pos, Quaternion.identity, parent).GetComponent<MenuElementRegion>();
        element.textName.richText = false;
        element.textName.text = name;
        element.textName.isRightToLeftText = false;
        element.textName.alignment = TextAlignmentOptions.MidlineLeft;
        element.textName.color = Color.white;
        element.textPing.richText = false;
        element.textPing.text = hint;
        element.textPing.alignment = TextAlignmentOptions.MidlineRight;
        element.textPing.color = hintColor;
        element.parentPage = page;
        RegionCode(element) = code;
        pos.y -= RowStep;
        return element;
    }

    /// <summary>A section title: a plain copy of a row's name text, not selectable.</summary>
    private static void AddLabel(TextMeshProUGUI template, Transform parent, Vector3 templateRowPos, Vector3 rowPos, string text)
    {
        var label = Object.Instantiate(template.gameObject, template.transform.parent, worldPositionStays: true);
        label.name = "Haulnet.Section";
        foreach (var component in label.GetComponents<Component>())
        {
            if (component is not Transform && component is not TextMeshProUGUI && component is not CanvasRenderer)
            {
                Object.Destroy(component);
            }
        }

        // Re-parent next to the rows so it does not move with one row's hover animation.
        label.transform.SetParent(parent, worldPositionStays: true);
        label.transform.position = template.transform.position + (rowPos - templateRowPos);
        var tmp = label.GetComponent<TextMeshProUGUI>();
        tmp.richText = false;
        tmp.text = text;
        tmp.color = Popups.Accent;
        tmp.fontSize = template.fontSize * 0.8f;
        tmp.raycastTarget = false;
    }

    /// <summary>Fills in ping, name and load of the server rows as the checks finish.</summary>
    private static IEnumerator UpdateServerRows(List<ServerRow> rows)
    {
        foreach (var row in rows)
        {
            row.Check = ServerCheck.RunAsync(row.Server.Address);
        }

        var deadline = Time.realtimeSinceStartup + CheckTimeoutSeconds;
        while (rows.Any(r => !r.Shown) && Time.realtimeSinceStartup < deadline)
        {
            foreach (var row in rows.Where(r => !r.Shown && r.Check!.IsCompleted))
            {
                row.Shown = true;
                if (row.Element == null)
                {
                    continue; // Page closed.
                }

                row.Status = row.Check!.IsFaulted ? null : row.Check.Result;
                ShowStatus(row);
            }

            yield return null;
        }

        foreach (var row in rows.Where(r => !r.Shown && r.Element != null))
        {
            row.Element.textPing.text = Text.NoPing;
            row.Element.textPing.color = Grey;
        }
    }

    private static void ShowStatus(ServerRow row)
    {
        var status = row.Status;
        var ping = row.Element.textPing;
        if (status == null || !status.Resolved)
        {
            ping.text = Text.NoPing;
            ping.color = PingBad;
            return;
        }

        if (row.Server.Name.Length == 0 && status.Name.Length > 0)
        {
            row.Element.textName.text = new KnownServer(row.Server.Address, status.Name, false).Display;
        }

        if (status.HasStatus && !status.Accepting)
        {
            ping.text = Text.HintFull;
            ping.color = PingBad;
            return;
        }

        SetPing(ping, status.PingMs);
    }

    /// <summary>Same thresholds and colours as the game's region rows.</summary>
    private static void SetPing(TextMeshProUGUI text, int ms)
    {
        if (ms < 0)
        {
            text.text = Text.NoPing;
            text.color = Grey;
        }
        else if (ms > 999)
        {
            text.text = ">999 " + Text.Ms;
            text.color = PingBad;
        }
        else
        {
            text.text = ms + " " + Text.Ms;
            text.color = ms < 50 ? PingGood : ms < 100 ? PingFair : PingPoor;
        }
    }

    // ---------- Choosing a row ----------

    private static bool BeforePickRegion(MenuPageRegions __instance, string _region)
    {
        if (_continuing)
        {
            return true; // Our own call below: let the game continue with the region code.
        }

        if (_region == null || !_region.StartsWith(CodePrefix, StringComparison.Ordinal))
        {
            Session.ChooseCloud(); // A Photon region: play through Photon Cloud.
            return true;
        }

        if (_busy)
        {
            return false;
        }

        try
        {
            Pick(__instance, _region);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Could not handle the Haulnet row '{_region}': {e}");
        }

        return false;
    }

    private static void Pick(MenuPageRegions page, string code)
    {
        if (code == CodeMyPc)
        {
            OpenPortPopup(page);
        }
        else if (code == CodeAddress)
        {
            OpenAddressPopup(page);
        }
        else if (code.StartsWith(CodeServer, StringComparison.Ordinal)
                 && int.TryParse(code.Substring(CodeServer.Length), out var index) && index >= 0 && index < Rows.Count)
        {
            var row = Rows[index];
            if (row.Status is { HasStatus: true, Accepting: false })
            {
                Popups.ShowNow(Text.FullHeader, Text.FullBody(row.Server.Address.Display));
                return;
            }

            var name = row.Server.Name.Length > 0 ? row.Server.Name : row.Status?.Name ?? "";
            Session.ChooseServer(row.Server.Address, name);
            Continue(page);
        }
    }

    /// <summary>
    /// "On my PC" works like Minecraft's "Open to LAN": the player sets the port (last one prefilled), the game
    /// takes that port and the next two, and the lobby shows them. How friends reach it is the player's business.
    /// </summary>
    private static void OpenPortPopup(MenuPageRegions page)
    {
        MenuManager.instance.PagePopUpTextInput(
            Text.PortHeader, null, Popups.Accent,
            Text.PortBody, null,
            Plugin.Settings.HostPort.ToString(), Text.Create, null,
            upperOnly: false, maxLength: 5, allowEmpty: false,
            text => OnPortEntered(page, text), null);
        _addressInput = MenuPagePopUpTextInput.instance != null ? MenuPagePopUpTextInput.instance.menuTextInput : null;
    }

    private static void OnPortEntered(MenuPageRegions page, string text)
    {
        _addressInput = null;
        text = (text ?? "").Trim();
        if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var port)
            || port < 1024 || port > 65533)
        {
            Popups.ShowNow(Text.PortInvalidHeader, Text.PortInvalidBody(text));
            return;
        }

        Plugin.Settings.HostPort = port;
        HostOnMyPc(page, port);
    }

    private static void HostOnMyPc(MenuPageRegions page, int port)
    {
        if (page == null)
        {
            return;
        }

        Session.ChooseOnMyPc(port);
        if (Plugin.Server.State == ServerState.Failed)
        {
            Popups.ShowNow(Text.ServerFailedHeader, Text.ServerFailedBody(Plugin.Server.Error));
            Session.ChooseCloud();
            return;
        }

        Continue(page);
    }

    /// <summary>
    /// Continue the game's own flow as if a region was picked: <c>PickRegion</c> sets the region and opens the saves
    /// page (private game) or the Public game page. Haulnet servers advertise one region, Photon code "eu"
    /// (<see cref="HostServer.RegionCode"/>); the code is never shown.
    /// </summary>
    private static void Continue(MenuPageRegions page)
    {
        _continuing = true;
        try
        {
            page.PickRegion(HostServer.RegionCode);
            MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Confirm, null, 1f, 1f);
        }
        finally
        {
            _continuing = false;
        }
    }

    // ---------- Server by address ----------

    private static void OpenAddressPopup(MenuPageRegions page)
    {
        var initial = _draft.Length > 0 ? _draft : Plugin.Settings.LastAddress;
        MenuManager.instance.PagePopUpTextInput(
            Text.AddressHeader, null, Popups.Accent,
            Text.AddressBody, null,
            initial, Text.Connect, null,
            upperOnly: false, maxLength: 100, allowEmpty: false,
            text => OnAddressEntered(page, text), null);
        _addressInput = MenuPagePopUpTextInput.instance != null ? MenuPagePopUpTextInput.instance.menuTextInput : null;
    }

    private static void OnAddressEntered(MenuPageRegions page, string text)
    {
        _addressInput = null;
        _draft = (text ?? "").Trim();
        if (!ServerAddress.TryParse(_draft, out var address, out var error))
        {
            Popups.ShowNow(Text.AddressInvalidHeader, Text.AddressInvalidBody(_draft, error));
            return;
        }

        Plugin.Settings.LastAddress = _draft;
        if (page == null)
        {
            return;
        }

        Plugin.Runner.StartCoroutine(CheckThenContinue(page, address));
    }

    private static IEnumerator CheckThenContinue(MenuPageRegions page, ServerAddress address)
    {
        _busy = true;
        if (_addressRow != null)
        {
            _addressRow.textPing.text = Text.Checking;
            _addressRow.textPing.color = Grey;
        }

        var check = ServerCheck.RunAsync(address);
        var deadline = Time.realtimeSinceStartup + CheckTimeoutSeconds;
        while (!check.IsCompleted && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        _busy = false;
        if (_addressRow != null)
        {
            _addressRow.textPing.text = "";
        }

        if (page == null)
        {
            yield break; // The player left the page meanwhile.
        }

        var status = check.IsCompleted && !check.IsFaulted ? check.Result : null;
        if (status != null && !status.Resolved)
        {
            Popups.ShowNow(Text.NotFoundHeader, Text.NotFoundBody(address.Host));
            yield break;
        }

        if (status is { HasStatus: true, Accepting: false })
        {
            Popups.ShowNow(Text.FullHeader, Text.FullBody(address.Display));
            yield break;
        }

        Plugin.Log.LogInfo($"Server by address {address}: ping {status?.PingMs ?? -1} ms, status endpoint {(status?.HasStatus == true ? "'" + status.Name + "' luxon " + status.Luxon : "absent")}.");
        Session.ChooseServer(address, status?.Name ?? "");
        Continue(page);
    }

    /// <summary>Ctrl+V for the address field. The game's text field only takes typed characters.</summary>
    private static void AfterTextInputUpdate(MenuTextInput __instance)
    {
        if (_addressInput == null || !ReferenceEquals(__instance, _addressInput))
        {
            return;
        }

        var text = TextCurrent(__instance) ?? "";
        var clean = new string(text.Where(c => !char.IsControl(c)).ToArray());
        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.V))
        {
            var clipboard = GUIUtility.systemCopyBuffer ?? "";
            clean += new string(clipboard.Where(c => !char.IsControl(c) && !char.IsWhiteSpace(c)).ToArray());
        }

        if (clean.Length > __instance.maxLength)
        {
            clean = clean.Substring(0, __instance.maxLength);
        }

        if (clean != text)
        {
            TextCurrent(__instance) = clean;
            InputTextSet!.Invoke(__instance, null);
        }
    }

    private sealed class ServerRow
    {
        public ServerRow(KnownServer server, MenuElementRegion element)
        {
            Server = server;
            Element = element;
        }

        public KnownServer Server { get; }
        public MenuElementRegion Element { get; }
        public System.Threading.Tasks.Task<ServerStatus>? Check { get; set; }
        public ServerStatus? Status { get; set; }
        public bool Shown { get; set; }
    }
}
