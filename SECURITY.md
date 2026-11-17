# Security policy

Haulnet redirects the game's network connection and, when you host "On my PC", starts a server on your machine, so
security reports matter.

## Reporting a vulnerability

Do not open a public issue. Use GitHub's
[private vulnerability reporting](https://github.com/haulnet/haulnet-repo/security/advisories/new), or write to
[security@haulnet.cc](mailto:security@haulnet.cc), with what you found and how to reproduce it. Expect an
acknowledgement within a week.

## Scope

- The Haulnet mod: the redirect, the way it starts and configures the bundled server, what it writes into the
  Steam lobby, and how it reads the server list and server status.
- The release packages published in this repository.

Reports about the server package belong to [haulnet-server](https://github.com/haulnet/haulnet-server), about the
public server list and its monitor to [haulnet-servers](https://github.com/haulnet/haulnet-servers); the same
address works for both. Vulnerabilities in LuxonServer itself go to the
[LuxonServer project](https://github.com/niansa/LuxonServer); Haulnet moves to a fixed release when there is one.

## Supported versions

Only the latest release gets fixes.
