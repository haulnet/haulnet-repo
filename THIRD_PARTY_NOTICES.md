# Third-party notices

Components that Haulnet redistributes, with their licenses. Full license texts are included in release
packages next to the component.

| Component | Version | License | Source | Redistributed in |
|---|---|---|---|---|
| LuxonServer | v1.5 (`luxon_server.msvc.release.exe`, SHA-256 pinned in `mod/Haulnet/Haulnet.csproj`) | BSD-3-Clause | https://github.com/niansa/LuxonServer | Mod package, `server/` folder, with its license as `LuxonServer-LICENSE.txt` |

The LuxonServer binary contains these libraries; their notices and full license texts ship next to it as
`server/LuxonServer-THIRD_PARTY_NOTICES.txt` (source: `mod/Haulnet/LuxonServer-THIRD_PARTY_NOTICES.txt`):

| Component | Version | License | Source |
|---|---|---|---|
| Luxon | commit `81458d7` | BSD-3-Clause | https://gitlab.com/luxon_project/Luxon |
| Mbed TLS | 3.6.5 | Apache-2.0 (chosen from Apache-2.0 OR GPL-2.0-or-later) | https://github.com/Mbed-TLS/mbedtls |
| spdlog | 1.17.0 | MIT | https://github.com/gabime/spdlog |
| JSON for Modern C++ (with code by Bjoern Hoehrmann and Florian Loitsch, MIT; Google Abseil, Apache-2.0; Hedley, CC0-1.0) | 3.12.0 | MIT | https://github.com/nlohmann/json |
| mini-yaml | as bundled | MIT | https://github.com/jimmiebergmann/mini-yaml |
| sqlite3pp | 1.0.10 | MIT | https://github.com/iwongu/sqlite3pp |
| magic_enum | 0.9.7 | MIT | https://github.com/Neargye/magic_enum |
| Boost.PFR | boost-1.91.0 | BSL-1.0 | https://github.com/boostorg/pfr |
| wepoll | as bundled | BSD-2-Clause | https://github.com/piscisaureus/wepoll |
| Chromium `flat_map` (adapted in Luxon) | — | BSD-3-Clause | https://chromium.googlesource.com/chromium/src/ |
| embedresource | 0.0.12 | BSD-3-Clause | https://github.com/ankurvdev/embedresource |
| commoncpp | 1.2.1 | Unlicense | https://gitlab.com/niansa/commoncpp |
| SQLite | 3.51.2 | Public domain | https://sqlite.org/ |

Not redistributed (installed separately by the player): BepInEx (LGPL-2.1), HarmonyX (MIT), the game and
its bundled Unity and Photon assemblies (proprietary).
