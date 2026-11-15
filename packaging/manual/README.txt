HAULNET for R.E.P.O.
====================

Play R.E.P.O. with friends on a server you control, instead of Photon Cloud:
on your own PC (like "Open to LAN"), on Haulnet servers (official and
community), or on any server by address. Until you pick a Haulnet place, the
game plays exactly as without the mod.

Useful when no Photon Cloud region is close to you (a Haulnet server can
stand in any city), when you want a server of your own, and for direct play
from your PC, like "Open to LAN": friends connect straight to the host's PC
over the home network, or over the internet through Radmin VPN, WireGuard or
a forwarded port.

Full guide: https://haulnet.cc/docs/install/


REQUIREMENTS
------------

- R.E.P.O. from Steam, on Windows.
- BepInExPack (BepInEx 5) installed in the game folder:
  https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/
- Everyone in a Haulnet game needs Haulnet and the same game version.

This zip is for installing by hand. Haulnet is planned for Thunderstore, so
that mod managers (r2modman, Gale, the Thunderstore app) can install it.


INSTALL
-------

1. Install BepInExPack into the game folder (the folder with REPO.exe) and
   start the game once, so BepInEx creates its folders. Close the game.
2. Extract this zip into REPO\BepInEx\plugins. The result must be:

     REPO\BepInEx\plugins\Haulnet\Haulnet.dll
     REPO\BepInEx\plugins\Haulnet\server\luxon_server.exe

   Keep the server folder next to Haulnet.dll; hosting needs it.
3. Start the game. REPO\BepInEx\LogOutput.log contains a line like
   "Haulnet <version> loaded."


FIRST RUN
---------

Open Private game or Public game: the region page now has two tabs,
HAULNET and PHOTON CLOUD (the game's own regions, unchanged). The HAULNET
tab lists, in this order:

- On my PC: host a private game on your PC. Enter a UDP port (27000 by
  default); the server uses it and the next two. Friends need a network path
  to your PC: Radmin VPN (https://www.radmin-vpn.com/), WireGuard
  (https://www.wireguard.com/), the same home network, IPv6 or a forwarded
  port.
- + Server by address...: type host or host:port of a server you or a
  friend runs.
- This PC: shown when a haulnet-server runs on your PC.
- Your saved servers, then the servers you played on recently, one row
  each with name or address; the right side shows the ping, "full",
  "updating", or "—" when the server does not answer.
- One row per city, e.g. Europe [Moscow]: Haulnet servers (official and
  community), with ping. Nobody needs to reach your PC there.

Friends join the usual way: accept the Steam invite or use "Join game" in
the Steam friends list.


WINDOWS FIREWALL AND PORTS
--------------------------

The first time you host "On my PC", Windows may ask whether luxon_server
and R.E.P.O. may use the network. Allow it for the networks your friends
come from (your home network, the Radmin VPN or WireGuard adapter). Without
it, friends get no answer from your PC.

- Hosting uses the port you enter and the next two: UDP 27000-27002 by
  default. Forward these on your router only if friends join over the
  internet without Radmin VPN or WireGuard.
- UDP 47777: optional address check between a friend and the host.


UPDATE AND REMOVE
-----------------

- Update: replace the BepInEx\plugins\Haulnet folder with the new one.
- Remove: delete BepInEx\plugins\Haulnet. Settings stay in
  BepInEx\config\haulnet.repo.cfg and the cached server list in
  BepInEx\cache\Haulnet until you delete them.

Haulnet never changes game files or system settings.


PROBLEMS
--------

- Every message Haulnet shows, explained:
  https://haulnet.cc/docs/troubleshooting/
- Report a bug: https://github.com/haulnet/haulnet-repo/issues
  Attach BepInEx\LogOutput.log and say what you did.
- Security reports: security@haulnet.cc


Haulnet is an independent fan project. It is not affiliated with or endorsed
by semiwork (developers of R.E.P.O.) or Exit Games GmbH. "R.E.P.O." and
"Photon" are trademarks of their respective owners and are used only to
describe compatibility.

MIT license (see LICENSE). Bundled components and their licenses:
THIRD_PARTY_NOTICES.md and server\LuxonServer-LICENSE.txt.
