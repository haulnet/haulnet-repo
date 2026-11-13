using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Haulnet;

/// <summary>
/// The game's region page becomes "where to play" (ADR 0015) with two tabs: HAULNET (On my PC, a haulnet-server
/// running on this PC, community servers from the registry, recent and saved servers, Server by address) and
/// PHOTON CLOUD (the game's own region rows, untouched). Choosing a row sets the target for this game and
/// continues the game's own flow exactly like a region would (saves page for a private game, the Public game page
/// for a public one).
/// </summary>
/// <remarks>
/// Hooks (game code), all optional; without them the page is the unmodded one and the game plays through Photon:
/// <list type="bullet">
/// <item><c>MenuPageRegions.Start</c> prefix: clear the previous choice before the page connects to Photon;
/// postfix: add the tabs and rows. The page instantiates its region rows only after Photon answers, from
/// <c>transformStartPosition</c> downwards, 32 units apart, into the same parent; both tabs share that space and
/// the hidden tab's rows are inactive.</item>
/// <item><c>MenuPageRegions.PickRegion</c> prefix: Haulnet rows carry codes <c>haulnet:*</c>, handled here.</item>
/// <item><c>MenuTextInput.Update</c> postfix: Ctrl+V in our popups (the game's text field has no paste).</item>
/// </list>
/// The tab buttons are copies of the page's own "Back" button (<c>MenuButton</c> with the persistent
/// <c>ExitPage</c> listener). If it cannot be found, both lists are shown one under the other with section titles.
/// </remarks>
internal static class WherePage
{
    private const string CodePrefix = "haulnet:";
    private const string CodeMyPc = "haulnet:mypc";
    private const string CodeAddress = "haulnet:address";
    private const string CodeRow = "haulnet:row:";
    private const float RowStep = 32f;
    private const float CheckTimeoutSeconds = 6f;

    // A server within this many ms of the best one in its city counts as "as close" when balancing private games.
    private const int SamePingMs = 15;

    private static readonly Color PingGood = new(0.2f, 0.8f, 0.2f);
    private static readonly Color PingFair = new(0.8f, 0.8f, 0.2f);
    private static readonly Color PingPoor = new(0.8f, 0.4f, 0.2f);
    private static readonly Color PingBad = new(0.8f, 0.2f, 0.2f);
    private static readonly Color Grey = new(0.6f, 0.6f, 0.6f);

    // Game internals: MenuPageRegions.type (internal enum), MenuElementRegion.regionCode,
    // MenuTextInput.textCurrent / InputTextSet, MenuScrollBox.RecalculateScrollHeight.
    private static readonly FieldInfo? PageTypeField = AccessTools.Field(typeof(MenuPageRegions), "type");
    private static readonly AccessTools.FieldRef<MenuElementRegion, string> RegionCode =
        AccessTools.FieldRefAccess<MenuElementRegion, string>("regionCode");
    private static readonly AccessTools.FieldRef<MenuTextInput, string> TextCurrent =
        AccessTools.FieldRefAccess<MenuTextInput, string>("textCurrent");
    private static readonly MethodInfo? InputTextSet = AccessTools.Method(typeof(MenuTextInput), "InputTextSet");
    private static readonly MethodInfo? RecalculateScroll = AccessTools.Method(typeof(MenuScrollBox), "RecalculateScrollHeight");

    private static readonly List<Row> Rows = new();
    private static readonly List<GameObject> HaulnetObjects = new();
    private static MenuPageRegions? _page;

    // Children of the list that existed before the page filled it (the inactive row template): never shown.
    private static readonly HashSet<Transform> Untouched = new();
    private static Vector3 _contentStart;
    private static MenuButton? _tabHaulnet;
    private static MenuButton? _tabPhoton;
    private static bool _photonTab;
    private static MenuElementRegion? _addressRow;
    private static MenuTextInput? _pasteInput;
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
            Plugin.Log.LogWarning("MenuTextInput.Update not found; Ctrl+V in Haulnet popups is off.");
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
            Setup(__instance);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Could not add Haulnet to the region page: {e}");
        }
    }

    private static bool IsHostFlow(MenuPageRegions page) => Convert.ToInt32(PageTypeField!.GetValue(page)) == 0;

    private static void Setup(MenuPageRegions page)
    {
        _page = page;
        _tabHaulnet = null;
        _tabPhoton = null;
        _busy = false;

        var start = page.transformStartPosition;
        var parent = start.parent;
        var tabPos = start.position;
        Untouched.Clear();
        foreach (Transform child in parent)
        {
            Untouched.Add(child);
        }

        _contentStart = tabPos - new Vector3(0f, RowStep, 0f);

        // The scroll box may switch rows on and off by visibility; the tabs decide visibility here.
        if (page.menuScrollBox != null)
        {
            page.menuScrollBox.disableHiddenChildren = false;
        }

        var tabsMade = MakeTabs(page, parent, tabPos);
        if (tabsMade)
        {
            // Both lists start under the tab buttons; the game places its rows from the start position.
            start.position = _contentStart;
            if (page.loadingGraphics != null)
            {
                page.loadingGraphics.transform.position -= new Vector3(0f, RowStep, 0f);
            }

            BuildHaulnetRows(page, sectionTitles: false);
            ShowTab(Plugin.Settings.LastTabPhoton, save: false);
            Plugin.Runner.StartCoroutine(KeepTabs(page));
        }
        else
        {
            // Fallback without tab buttons: Haulnet rows with section titles above the Photon rows.
            BuildHaulnetRows(page, sectionTitles: true);
        }

        // The page reveals its list only after Photon answered; ours must be usable before that.
        if (page.scrollCanvasGroup != null)
        {
            page.scrollCanvasGroup.alpha = 1f;
        }

        Plugin.Runner.StartCoroutine(Registry.Refresh(changed =>
        {
            if (changed && _page == page && page != null)
            {
                BuildHaulnetRows(page, sectionTitles: !tabsMade);
                if (tabsMade)
                {
                    ShowTab(_photonTab, save: false);
                }
            }
        }));
    }

    /// <summary>(Re)creates the Haulnet rows. Called again when a fresh server list arrives.</summary>
    private static void BuildHaulnetRows(MenuPageRegions page, bool sectionTitles)
    {
        foreach (var obj in HaulnetObjects.Where(o => o != null))
        {
            Object.Destroy(obj);
        }

        HaulnetObjects.Clear();
        Rows.Clear();
        _addressRow = null;

        var parent = page.transformStartPosition.parent;
        var pos = sectionTitles ? page.transformStartPosition.position : _contentStart;
        var titlePos = pos;
        if (sectionTitles)
        {
            pos.y -= RowStep;
        }

        var elements = new List<MenuElementRegion>();
        if (IsHostFlow(page))
        {
            elements.Add(AddRow(page, parent, ref pos, CodeMyPc, Text.RowOnMyPc, $"UDP {Plugin.Settings.HostPort}", Grey));
        }

        // A haulnet-server running on this PC (its status endpoint answers on loopback).
        var loopback = ServerAddress.FromIp(System.Net.IPAddress.Loopback, 0);
        var local = LocalServer();
        if (local != null)
        {
            var row = new Row(Text.RowThisPc(local.Name), loopback, null) { SavedName = local.Name, KeepName = true };
            row.Status = local;
            row.Element = AddRow(page, parent, ref pos, CodeRow + Rows.Count, row.Name, "", Grey);
            Rows.Add(row);
            elements.Add(row.Element);
            ShowStatus(row);
        }

        var registryAddresses = new List<ServerAddress>();
        foreach (var city in Registry.Cities)
        {
            var row = new Row(city.DisplayName, null, city);
            row.Element = AddRow(page, parent, ref pos, CodeRow + Rows.Count, city.DisplayName, Text.Checking, Grey);
            Rows.Add(row);
            elements.Add(row.Element);
            registryAddresses.AddRange(city.Servers.Select(s => s.Address));
        }

        foreach (var known in KnownServers.All())
        {
            if (registryAddresses.Any(a => a.SameAs(known.Address)) || (local != null && known.Address.SameAs(loopback)))
            {
                continue; // Already listed as a community server or as this PC's server.
            }

            var row = new Row(known.Display, known.Address, null) { SavedName = known.Name };
            row.Element = AddRow(page, parent, ref pos, CodeRow + Rows.Count, known.Display, Text.Checking, Grey);
            Rows.Add(row);
            elements.Add(row.Element);
        }

        _addressRow = AddRow(page, parent, ref pos, CodeAddress, Text.RowByAddress, "", Grey);
        elements.Add(_addressRow);

        if (sectionTitles)
        {
            var template = elements[0];
            AddLabel(template.textName, parent, template.transform.position, titlePos, Text.SectionHaulnet);
            AddLabel(template.textName, parent, template.transform.position, pos, Text.SectionPhoton);
            pos.y -= RowStep;
            var shift = page.transformStartPosition.position.y - pos.y;
            page.transformStartPosition.position = pos;
            if (page.loadingGraphics != null)
            {
                page.loadingGraphics.transform.position -= new Vector3(0f, shift, 0f);
            }
        }

        Plugin.Runner.StartCoroutine(CheckRows(Rows.Where(r => r.Status == null).ToList(), IsHostFlow(page)));
        Plugin.Log.LogInfo($"Region page: {elements.Count} Haulnet row(s): {Registry.Cities.Count} city(ies), {(local != null ? "a" : "no")} local haulnet-server.");
    }

    private static ServerStatus? LocalServer()
    {
        try
        {
            var check = ServerCheck.RunAsync(ServerAddress.FromIp(System.Net.IPAddress.Loopback, 0));
            // Loopback answers or refuses at once; do not hold the menu for long either way.
            if (check.Wait(TimeSpan.FromMilliseconds(600)) && check.Result.HasStatus)
            {
                return check.Result;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug($"Local status check failed: {e.Message}");
        }

        return null;
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
        HaulnetObjects.Add(element.gameObject);
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
        HaulnetObjects.Add(label);
    }

    // ---------- Tabs ----------

    /// <summary>Two copies of the page's "Back" button, side by side at the top of the list.</summary>
    private static bool MakeTabs(MenuPageRegions page, Transform parent, Vector3 pos)
    {
        try
        {
            var back = page.GetComponentsInChildren<MenuButton>(true).FirstOrDefault(IsBackButton);
            if (back == null)
            {
                Plugin.Log.LogWarning("Region page: Back button not found; showing Haulnet rows without tabs.");
                return false;
            }

            _tabHaulnet = CloneButton(back, parent, pos, Text.SectionHaulnet, () => ShowTab(false, save: true));

            // The second tab starts where the first one's text ends, plus a gap.
            var width = ((RectTransform)_tabHaulnet.transform).rect.width * _tabHaulnet.transform.lossyScale.x;
            var gap = RowStep * _tabHaulnet.transform.lossyScale.x;
            _tabPhoton = CloneButton(back, parent, pos + new Vector3(width + gap, 0f, 0f), Text.SectionPhoton, () => ShowTab(true, save: true));
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"Region page: could not make tabs ({e.Message}); showing Haulnet rows without tabs.");
            return false;
        }
    }

    private static bool IsBackButton(MenuButton button)
    {
        var ui = button.GetComponent<Button>();
        if (ui == null)
        {
            return false;
        }

        for (var i = 0; i < ui.onClick.GetPersistentEventCount(); i++)
        {
            if (ui.onClick.GetPersistentMethodName(i) == nameof(MenuPageRegions.ExitPage))
            {
                return true;
            }
        }

        return false;
    }

    private static MenuButton CloneButton(MenuButton template, Transform parent, Vector3 position, string text, Action onClick)
    {
        // Clone while the template is inactive, so the copy's Awake runs after we set text and position.
        var wasActive = template.gameObject.activeSelf;
        template.gameObject.SetActive(false);
        var copy = Object.Instantiate(template.gameObject, parent, worldPositionStays: true);
        template.gameObject.SetActive(wasActive);

        copy.name = "Haulnet.Tab " + text;
        copy.transform.position = position;
        // The game's LocalizationChangedEvent writes the localized "Back" into the text on Start; ours is fixed.
        foreach (var localizer in copy.GetComponentsInChildren<LocalizationChangedEvent>(true))
        {
            Object.DestroyImmediate(localizer);
        }

        var button = copy.GetComponent<MenuButton>();
        button.buttonTextString = text;
        button.customColors = true;
        var ui = copy.GetComponent<Button>();
        ui.onClick = new Button.ButtonClickedEvent();
        ui.onClick.AddListener(() => onClick());
        copy.SetActive(true);
        return button;
    }

    private static void ShowTab(bool photon, bool save)
    {
        _photonTab = photon;
        if (save)
        {
            Plugin.Settings.LastTabPhoton = photon;
        }

        foreach (var obj in HaulnetObjects.Where(o => o != null))
        {
            obj.SetActive(!photon);
        }

        StyleTab(_tabHaulnet, !photon);
        StyleTab(_tabPhoton, photon);
        ApplyPhotonVisibility();
        Recalculate();
    }

    private static void StyleTab(MenuButton? tab, bool selected)
    {
        if (tab == null)
        {
            return;
        }

        tab.colorNormal = selected ? Popups.Accent : Color.gray;
        tab.colorHover = selected ? Popups.Accent : Color.white;
        tab.colorClick = Popups.Accent;
    }

    /// <summary>The game adds its region rows later; keep them on the Photon tab only.</summary>
    private static IEnumerator KeepTabs(MenuPageRegions page)
    {
        var lastCount = -1;
        while (page != null && _page == page)
        {
            var count = page.transformStartPosition.parent.childCount;
            if (count != lastCount)
            {
                lastCount = count;
                ApplyPhotonVisibility();
                Recalculate();
            }

            yield return null;
        }
    }

    private static void ApplyPhotonVisibility()
    {
        var page = _page;
        if (page == null)
        {
            return;
        }

        foreach (Transform child in page.transformStartPosition.parent)
        {
            var region = child.GetComponent<MenuElementRegion>();
            if (region != null && !Untouched.Contains(child) && !(RegionCode(region) ?? "").StartsWith(CodePrefix, StringComparison.Ordinal))
            {
                child.gameObject.SetActive(_photonTab);
            }
        }

        if (page.loadingGraphics != null && page.loadingGraphics.gameObject.activeSelf != _photonTab && !LoadingFinished(page))
        {
            page.loadingGraphics.gameObject.SetActive(_photonTab);
        }
    }

    // The page's coroutine fades its list in once Photon answered (alpha goes up); until then loading is shown.
    private static bool LoadingFinished(MenuPageRegions page) =>
        page.transformStartPosition.parent.Cast<Transform>()
            .Any(t => !Untouched.Contains(t) && t.GetComponent<MenuElementRegion>() is { } r && !(RegionCode(r) ?? "").StartsWith(CodePrefix, StringComparison.Ordinal));

    private static void Recalculate()
    {
        var box = _page != null ? _page.menuScrollBox : null;
        if (box == null || RecalculateScroll == null)
        {
            return;
        }

        try
        {
            RecalculateScroll.Invoke(box, new object[] { false });
        }
        catch (Exception e)
        {
            Plugin.Log.LogDebug($"Scroll height not recalculated: {e.Message}");
        }
    }

    // ---------- Checking servers ----------

    /// <summary>Fills in ping, name and load as the checks finish; picks the server of each city.</summary>
    private static IEnumerator CheckRows(List<Row> rows, bool hostFlow)
    {
        foreach (var row in rows)
        {
            row.Checks = row.City != null
                ? row.City.Servers.Select(s => ServerCheck.RunAsync(s.Address, s.StatusPort)).ToList()
                : new List<Task<ServerStatus>> { ServerCheck.RunAsync(row.Address!.Value) };
        }

        var deadline = Time.realtimeSinceStartup + CheckTimeoutSeconds;
        while (rows.Any(r => !r.Shown) && Time.realtimeSinceStartup < deadline)
        {
            foreach (var row in rows.Where(r => !r.Shown && r.Checks!.All(c => c.IsCompleted)))
            {
                row.Shown = true;
                if (row.Element == null)
                {
                    continue; // Page closed or rows rebuilt.
                }

                if (row.City != null)
                {
                    PickCityServer(row, hostFlow);
                }
                else
                {
                    row.Status = row.Checks![0].IsFaulted ? null : row.Checks[0].Result;
                }

                ShowStatus(row);
            }

            yield return null;
        }

        foreach (var row in rows.Where(r => !r.Shown && r.Element != null))
        {
            row.Shown = true;
            if (row.City != null)
            {
                PickCityServer(row, hostFlow);
                ShowStatus(row);
            }
            else
            {
                row.Element.textPing.text = Text.NoPing;
                row.Element.textPing.color = Grey;
            }
        }
    }

    /// <summary>
    /// A city may have several servers. Usable: status endpoint answers, accepts new games, runs a LuxonServer
    /// version the list marks compatible. Private games go to the least loaded of the closest servers; public
    /// games go to the busiest one, so public players of a city meet on one server.
    /// </summary>
    private static void PickCityServer(Row row, bool hostFlow)
    {
        var compatible = Registry.CompatibleLuxon;
        var usable = row.City!.Servers
            .Select((server, i) => (server, status: row.Checks![i].IsCompleted && !row.Checks[i].IsFaulted ? row.Checks[i].Result : null))
            .Where(x => x.status is { HasStatus: true, Accepting: true }
                        && (compatible.Count == 0 || compatible.Contains(x.status.Luxon)))
            .ToList();

        if (usable.Count == 0)
        {
            row.Chosen = null;
            row.Status = null;
            Plugin.Log.LogInfo($"{row.City.DisplayName}: no usable server.");
            return;
        }

        (RegistryServer server, ServerStatus? status) chosen;
        if (hostFlow)
        {
            var best = usable.Min(x => Ping(x.status!));
            chosen = usable.Where(x => Ping(x.status!) <= best + SamePingMs).OrderBy(x => x.status!.Players).ThenBy(x => Ping(x.status!)).First();
        }
        else
        {
            chosen = usable.OrderByDescending(x => x.status!.Players).ThenBy(x => Ping(x.status!)).First();
        }

        row.Chosen = chosen.server;
        row.Status = chosen.status;
        Plugin.Log.LogInfo($"{row.City.DisplayName}: {usable.Count} usable server(s), chose {chosen.server.Id} ({chosen.status!.Players} players, {chosen.status.PingMs} ms).");
    }

    private static int Ping(ServerStatus status) => status.PingMs >= 0 ? status.PingMs : int.MaxValue / 2;

    private static void ShowStatus(Row row)
    {
        var status = row.Status;
        var ping = row.Element.textPing;
        if (status == null || !status.Resolved)
        {
            ping.text = Text.NoPing;
            ping.color = PingBad;
            return;
        }

        if (row.City == null && !row.KeepName && row.SavedName.Length == 0 && status.Name.Length > 0 && row.Address.HasValue)
        {
            row.Element.textName.text = new KnownServer(row.Address.Value, status.Name, false).Display;
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
            return;
        }

        if (code == CodeAddress)
        {
            OpenAddressPopup(page);
            return;
        }

        if (!code.StartsWith(CodeRow, StringComparison.Ordinal)
            || !int.TryParse(code.Substring(CodeRow.Length), out var index) || index < 0 || index >= Rows.Count)
        {
            return;
        }

        var row = Rows[index];
        if (row.City != null)
        {
            if (!row.Shown)
            {
                return; // Still checking its servers.
            }

            if (row.Chosen == null)
            {
                Popups.ShowNow(Text.NoServerHeader, Text.NoServerBody(row.City.DisplayName));
                return;
            }

            Session.ChooseServer(row.Chosen.Address, row.Chosen.DisplayName);
            Continue(page);
            return;
        }

        if (row.Status is { HasStatus: true, Accepting: false })
        {
            Popups.ShowNow(Text.FullHeader, Text.FullBody(row.Address!.Value.Display));
            return;
        }

        var name = row.SavedName.Length > 0 ? row.SavedName : row.Status?.Name ?? "";
        Session.ChooseServer(row.Address!.Value, name);
        Continue(page);
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
        _pasteInput = MenuPagePopUpTextInput.instance != null ? MenuPagePopUpTextInput.instance.menuTextInput : null;
    }

    private static void OnPortEntered(MenuPageRegions page, string text)
    {
        _pasteInput = null;
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
            _page = null; // Stop the tab keeper; the page closes now.
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
        _pasteInput = MenuPagePopUpTextInput.instance != null ? MenuPagePopUpTextInput.instance.menuTextInput : null;
    }

    private static void OnAddressEntered(MenuPageRegions page, string text)
    {
        _pasteInput = null;
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

    /// <summary>Ctrl+V for our popups' text field. The game's text field only takes typed characters.</summary>
    private static void AfterTextInputUpdate(MenuTextInput __instance)
    {
        if (_pasteInput == null || !ReferenceEquals(__instance, _pasteInput))
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

    private sealed class Row
    {
        public Row(string name, ServerAddress? address, RegistryCity? city)
        {
            Name = name;
            Address = address;
            City = city;
        }

        public string Name { get; }

        /// <summary>Server of a single-server row; null for a city row.</summary>
        public ServerAddress? Address { get; }

        /// <summary>City of a community row; null otherwise.</summary>
        public RegistryCity? City { get; }

        public string SavedName { get; set; } = "";

        /// <summary>The row text is final (this PC's server); do not replace it with the status name.</summary>
        public bool KeepName { get; set; }
        public MenuElementRegion Element { get; set; } = null!;
        public List<Task<ServerStatus>>? Checks { get; set; }
        public ServerStatus? Status { get; set; }

        /// <summary>The server of the city this game would go to.</summary>
        public RegistryServer? Chosen { get; set; }

        public bool Shown { get; set; }
    }
}
