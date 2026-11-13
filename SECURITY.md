# Security policy

SemiLink starts a network server on the host's machine, so security reports matter.

## Reporting a vulnerability

Do not open a public issue. Use GitHub's
[private vulnerability reporting](https://github.com/belomaxorka/repo-lan/security/advisories/new) instead.
Expect an acknowledgement within a week.

Vulnerabilities in LuxonServer itself should be reported to the
[LuxonServer project](https://github.com/niansa/LuxonServer); we will update the pinned version once fixed.

## Scope

- The SemiLink mod and the way it starts and configures the server.
- Our Docker image and compose files.

## Hardening guidance for hosts

- Prefer a virtual LAN (Radmin VPN, ZeroTier, Tailscale) over exposing ports to the internet.
- If you forward ports, forward only the UDP ports the server needs and stop the server when not playing.
