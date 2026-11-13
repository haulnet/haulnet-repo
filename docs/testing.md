# Testing checklist

Multiplayer cannot be verified by one person or by CI. Every release, game update and LuxonServer bump is
checked **manually by at least two players on two PCs** with this checklist. Results go to
`docs/test-reports/YYYY-MM-DD-<topic>.md` with game, SemiLink and LuxonServer versions.

## Environment

- [ ] Game version, SemiLink version, LuxonServer version recorded
- [ ] Network: same LAN / different homes; ISP and router type; active tunnel path shown by the mod
      (LAN, IPv6, UPnP, hole-punched, relayed, manual)
- [ ] Host OS and client OS

## Core flow

- [ ] Host mode starts the server; the address shown is reachable by the friend
- [ ] Friend joins by address
- [ ] Friend joins through a Steam invite (when phase 4 lands)
- [ ] Lobby → truck → level → extraction → shop → next level
- [ ] Voice chat both ways, including proximity
- [ ] Item and enemy synchronisation looks normal (no desync, no rubber-banding beyond network latency)
- [ ] Friend leaves and rejoins mid-session
- [ ] Host quits: server stops, no orphan process left

## Latency

- [ ] In-game ping (or a ping mod) through Photon Cloud vs SemiLink, same players, same time of day

## Regression

- [ ] Cloud mode: public play works exactly as without the mod
- [ ] Solo mode unaffected
- [ ] Game starts cleanly with the mod installed but disabled

## Failure handling

- [ ] Server port already in use → clear message
- [ ] Wrong address on Join → clear message, no infinite spinner
- [ ] Firewall blocks the server → clear message with a fix
