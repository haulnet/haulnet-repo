# 01. Что уже существует (исследование от 13.11.2026)

Вывод: **готового «всё в одном» нет**, но все кирпичи уже лежат. Есть открытый сервер,
совместимый с Photon, и есть клиентские моды-редиректы. Нет одного: простой сборки, где игрок
ставит мод, нажимает «Хост» и зовёт друга.

## Серверы (замена Photon Cloud)

| Проект | Что это | Лицензия Photon | Docker | Голос | Статус |
|---|---|---|---|---|---|
| [niansa/LuxonServer](https://github.com/niansa/LuxonServer) | Clean-room реализация Photon Realtime (Name/Master/Game server), C++23, BSD-3 | Не нужна | Не упомянут | Не заявлен, **проверить** | 383 коммита; в `compatibility.txt`: «R.E.P.O. — works flawlessly». Windows, Linux, macOS, BSD |
| [openpak/photon](https://github.com/openpak/photon) | Photon-совместимый сервер на .NET (Realtime, Fusion, Quantum 2), AGPL-3.0 | Не нужна | Есть (`ghcr.io/openpak/photon`) | Не заявлен | Ориентирован на Switch-игры, R.E.P.O. не упоминается, 0 звёзд |
| [nyxikitty/photon-server-nodejs](https://github.com/nyxikitty/photon-server-nodejs) | Node.js сервер на старом протоколе GpBinaryV16 | Не нужна | Пример Dockerfile | Нет | Писался под старый VRChat, PUN 2 / Protocol18 под вопросом |
| Официальный Photon Server v5 ([гайд Modac](https://gist.github.com/Modac/01bd2b7a997a9fa7c36b0d8b548e7c47)) | Оригинальный сервер Exit Games | **Нужна**: в гайде обход проверки лицензии, дистрибутив с archive.org уже удалён | Нет ([Dockerized-Photon](https://github.com/GillesZunino/Dockerized-Photon) — Windows-контейнер) | Да, по отзывам | Юридически и технически плохой путь, **не используем** |

## Клиентские моды для R.E.P.O.

| Мод | Что делает | Минус |
|---|---|---|
| [zyxterus / Self Hosted Photon](https://thunderstore.io/c/repo/p/zyxterus/Self_Hosted_Photon/) | Подключение к своему серверу по IP/хосту, настройка в игре | v0.1.0, «not yet finished»; сервер предлагает поднимать официальный |
| Dev1A3 / Photon Server Settings (из гайда Modac) | Задаёт в игре адрес, порт, протокол и версию сервера | Только настройки, без сервера и без «хоста в один клик» |
| [Hiradpi / Custom-Photon-Server](https://github.com/Hiradpi/Custom-Photon-Server) | Подмена AppId Photon | Это просто свой аккаунт в облаке Photon, пинг тот же |
| [ArifRios1st / RepoCustomPhotonServer](https://github.com/ArifRios1st/RepoCustomPhotonServer) | Подмена AppId и региона | То же самое: облако Photon и регистрация на их сайте |
| [Zehs / LocalMultiplayer](https://thunderstore.io/c/repo/p/Zehs/LocalMultiplayer/) | Два экземпляра игры на **одном** Steam-аккаунте, через свои AppId в облаке Photon | Это и есть мод «ограничен одним Steam-аккаунтом»: он вообще для другой задачи (локальный co-op/тест) |

## Сообщество

- [Steam: «We still need LAN/P2P»](https://steamcommunity.com/app/3241660/discussions/0/838375928581129216/):
  игроки просят LAN; редиректы на свой AppId проблему не решают, потому что всё равно используется Photon.
- PCGamingWiki: нативного LAN у игры нет.

## Что из этого берём

1. **Сервер: LuxonServer** как upstream (BSD-3 позволяет включить его в сборку с указанием авторства).
   Мы его не форкаем ради правок, а пакуем: готовый конфиг, Docker-образ, Windows-сборка.
2. **Клиент: свой мод.** Существующие моды настраивают адрес, но не дают «Хоста в один клик» и
   не встраивают сервер. Идеи по патчу `AppSettings` берём у них, но код пишем сами.
3. Если LuxonServer не потянет голос (Photon Voice) или что-то в R.E.P.O. сломается, правим и
   отправляем PR в upstream, а не держим свой форк.
