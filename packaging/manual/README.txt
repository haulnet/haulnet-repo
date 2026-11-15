HAULNET for R.E.P.O.
====================

Play R.E.P.O. with friends on a server you control, instead of Photon Cloud:
on your own PC (like "Open to LAN"), on a community server, or on any server
by address. Until you pick a Haulnet place, the game plays exactly as without
the mod.

Full guide: https://haulnet.cc/docs/install/


REQUIREMENTS
------------

- R.E.P.O. from Steam.
- BepInExPack (BepInEx 5) installed in the game folder:
  https://thunderstore.io/c/repo/p/BepInEx/BepInExPack/
- Everyone in a Haulnet game needs Haulnet and the same game version.

A mod manager (r2modman, Gale, Thunderstore app) installs Haulnet and
BepInExPack by itself; this zip is for installing by hand.


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
HAULNET and PHOTON CLOUD (the game's own regions, unchanged).

- On my PC: host a private game on your PC. Enter a UDP port (27000 by
  default); the server uses it and the next two. Friends reach your PC over
  the same network, a VPN you already use, IPv6 or a forwarded port.
- Community servers: one row per city, with ping.
- + Server by address...: type host or host:port of a server you or a
  friend runs.

Friends join the usual way: accept the Steam invite or use Join friend.


WINDOWS FIREWALL AND PORTS
--------------------------

The first time you host "On my PC", Windows may ask whether luxon_server
and R.E.P.O. may use the network. Allow it for the networks your friends
come from (private networks for a LAN or most VPNs). Without it, friends get
no answer from your PC.

- Hosting uses the port you enter and the next two: UDP 27000-27002 by
  default. Forward these on your router only if friends join over the
  internet without a VPN.
- UDP 47777: optional address check between a friend and the host.


UPDATE AND REMOVE
-----------------

- Update: replace the BepInEx\plugins\Haulnet folder with the new one.
- Remove: delete BepInEx\plugins\Haulnet. Settings stay in
  BepInEx\config\haulnet.repo.cfg until you delete it.

Haulnet never changes game files or system settings.


PROBLEMS
--------

- Every message Haulnet shows, explained:
  https://haulnet.cc/docs/troubleshooting/
- Report a bug: https://github.com/haulnet/haulnet-repo/issues
  Attach BepInEx\LogOutput.log and say what you did.
- Security reports: security@haulnet.cc


Haulnet is an independent fan project, MIT-licensed (see LICENSE). It is not
affiliated with or endorsed by semiwork (developers of R.E.P.O.) or Exit
Games GmbH. "R.E.P.O." and "Photon" are trademarks of their respective
owners and are used only to describe compatibility. Bundled components and
their licenses: THIRD_PARTY_NOTICES.md and server\LuxonServer-LICENSE.txt.
