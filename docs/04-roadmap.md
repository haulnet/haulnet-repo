# 04. План работ

## Этап 0. Проверка гипотезы (1 вечер, вручную, без своего кода)

Цель: убедиться, что LuxonServer и R.E.P.O. реально работают у нас.

1. Хост скачивает релиз LuxonServer, в `config.yml` ставит `external_address` = свой Radmin-IP.
2. Оба игрока прописывают в `hosts` строку `<Radmin-IP хоста> ns.exitgames.com ns.photonengine.io`
   (временно, только для теста) или ставят мод Photon Server Settings.
3. Проверяем: создание лобби, вход друга, пинг в игре, **голосовой чат**, смену уровня, магазин,
   выход и повторный вход, Steam-инвайт.
4. Пишем результат в `docs/test-reports/`.

Если голос или что-то ещё не работает, это первый кандидат на PR в LuxonServer.

## Этап 1. Клиентский мод MVP

- Проект BepInEx-плагина (C#, netstandard2.1), ссылки на dll игры из локальной папки (в git не попадают).
- Конфиг: режим (Cloud / Connect), адрес, порт.
- Harmony-патч `AppSettings` для Realtime и Voice.
- Меню в игре (MenuLib/REPOConfig или своё).
- Результат: при запущенном вручную сервере друг подключается без `hosts`.

## Этап 2. «Хост в один клик»

- `luxon.exe` и конфиг лежат в пакете мода.
- Кнопка «Хост»: запуск сервера, автоподстановка `external_address` (поиск Radmin, ZeroTier или Tailscale по
  интерфейсам, иначе выбор вручную), отображение «скажи другу: 26.x.x.x», остановка при выходе.
- Проверка, что порт свободен, и понятные ошибки (брандмауэр Windows: предложить правило).

## Этап 3. Отдельный сервер и Docker

- `server/Dockerfile` (сборка LuxonServer из исходников на Debian slim), `docker-compose.yml`,
  `network_mode: host` или проброс UDP-портов, переменная `EXTERNAL_ADDRESS`.
- Образ в GitHub Container Registry, сборка через GitHub Actions.
- Windows-архив для VPS: `start.bat` + конфиг.

## Этап 4. Steam-инвайты и полировка

- Передача адреса сервера через данные Steam-лобби; авто-подключение у друга с модом.
- Сообщение «у хоста REPO LAN, поставь мод», если у друга мода нет.
- Публикация на Thunderstore (иконка, manifest.json, зависимости BepInExPack).

## Этап 5. Поддержка

- GitHub Actions: сборка мода и сервера, релизы по тегам.
- Проверка после каждого патча R.E.P.O.: смоук-тест по чек-листу из этапа 0.

## Структура репозитория (план)

```text
repo-lan/
├── README.md               # для игроков: что это и как поставить за 2 минуты
├── docs/                   # эта база знаний
├── mod/                    # BepInEx-плагин RepoLan (C#)
├── server/
│   ├── Dockerfile
│   ├── docker-compose.yml
│   └── config.yml          # шаблон конфига LuxonServer
├── packaging/thunderstore/ # manifest.json, icon.png, README для Thunderstore
└── .github/workflows/      # сборка и релизы
```
