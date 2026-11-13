# 02. Архитектура

## Как сейчас (Photon Cloud)

```mermaid
flowchart LR
    A[Игрок A<br/>Россия] -- 60-120 мс --> P[(Photon Cloud<br/>регион EU)]
    B[Игрок B<br/>Россия] -- 60-120 мс --> P
    P -- ретрансляция<br/>каждого пакета --> A
    P --> B
```

Пакеты A→B идут через сервер Photon. Даже если игроки сидят в соседних домах, задержка равна
сумме двух «плеч» до Европы. В соло PUN работает в offline mode, поэтому пинг 0.

## Как будет (REPO LAN)

```mermaid
flowchart LR
    subgraph host[ПК хоста]
        GA[R.E.P.O. + мод RepoLan]
        S[[Сервер<br/>LuxonServer]]
        GA -- 127.0.0.1 --> S
    end
    GB[Игрок B<br/>R.E.P.O. + мод RepoLan] -- Radmin / ZeroTier /<br/>Tailscale / проброс порта --> S
    GC[Игрок C] --> S
```

Игра думает, что говорит с Photon, но на деле общается с сервером на ПК хоста или на домашнем сервере.

## Компоненты

```mermaid
flowchart TB
    subgraph mod[Клиентский мод RepoLan — BepInEx-плагин, ставит каждый игрок]
        UI[Меню в игре:<br/>Хост / Подключиться по IP / Облако]
        PATCH[Harmony-патч подключения:<br/>подмена AppSettings PUN]
        LAUNCH[Запуск встроенного сервера<br/>как дочернего процесса]
    end
    subgraph srv[Сервер — один из трёх вариантов]
        E[Встроенный в мод<br/>luxon.exe рядом с dll]
        W[Отдельный Windows-exe<br/>для своего ПК/VPS]
        D[Docker-образ<br/>docker compose up -d]
    end
    UI --> PATCH
    UI --> LAUNCH --> E
    PATCH -. NameServer UDP 5058 .-> E & W & D
```

### 1. Клиентский мод (`RepoLan.dll`, BepInEx)

- Режимы: **Облако** (как сейчас, ничего не трогаем), **Хост**, **Подключиться** (ввести адрес).
- Патч через Harmony перед `PhotonNetwork.ConnectUsingSettings`:
  - `AppSettings.Server` ← адрес сервера, `Port` ← 5058 (NameServer), `UseNameServer = true`;
  - `FixedRegion` и AppId заполняем заглушками, сервер их не проверяет;
  - то же для Photon Voice (`AppIdVoice`), если голос идёт отдельным клиентом.
- **Без правки `hosts`**: LuxonServer предлагает перенаправлять `ns.photonengine.io` через hosts,
  но это требует админ-прав и ломает облачную игру во всех Photon-играх. Мод делает это точечно, только в R.E.P.O.
- В режиме «Хост» мод сам запускает сервер, показывает адрес, который нужно сказать другу
  (определяет IP Radmin, ZeroTier или Tailscale по сетевым интерфейсам), и гасит сервер при выходе из игры.

### 2. Сервер

Один и тот же LuxonServer с нашим `config.yml`, три способа поставки:

| Вариант | Для кого | Как запустить |
|---|---|---|
| Встроенный | Обычный игрок | Кнопка «Хост» в игре |
| Windows-exe | Отдельный ПК или VPS на Windows | Распаковать архив, запустить `start.bat` |
| Docker | Домашний сервер, NAS, Linux-VPS | `docker compose up -d` |

Важная настройка: `external_address`. NameServer отдаёт клиентам адрес Game-сервера,
поэтому там должен быть IP, по которому сервер видят **другие** игроки (адрес в Radmin или
публичный IP), а не `127.0.0.1`. Встроенный режим подставляет его автоматически.

Порты (UDP): 5058 (NameServer), 5055 (Master), 5056 (Game) плюс запасные 27000–27002.

## Последовательность подключения

```mermaid
sequenceDiagram
    participant G as Игра (мод RepoLan)
    participant N as NameServer :5058
    participant M as MasterServer :5055
    participant GS as GameServer :5056
    G->>N: Authenticate (AppId-заглушка, регион)
    N-->>G: адрес Master = external_address:5055
    G->>M: Authenticate, JoinLobby
    G->>M: CreateRoom / JoinRoom
    M-->>G: адрес GameServer
    G->>GS: Join, дальше RaiseEvent / RPC / свойства
    Note over G,GS: Весь игровой трафик идёт напрямую на сервер хоста
```

## Сетевые сценарии

```mermaid
flowchart LR
    subgraph v1[Radmin / ZeroTier / Tailscale — рекомендуем]
        a1[Хост 26.x.x.x] --- a2[Друг 26.y.y.y]
    end
    subgraph v2[Проброс порта]
        b1[Хост: UDP 5055-5058<br/>проброшены на роутере] --- b2[Друг по публичному IP]
    end
    subgraph v3[Сервер в Docker на VPS или домашнем сервере]
        c0[(VPS)] --- c1[Хост]
        c0 --- c2[Друг]
    end
```

- **Виртуальная локалка** работает без настройки роутера, даже за CGNAT. Минус: всем нужно поставить
  одну и ту же программу. Tailscale и ZeroTier обычно строят прямой P2P-туннель; Radmin иногда идёт через свой relay.
- **Проброс порта** даёт минимальный пинг, но нужен белый IP и доступ к роутеру.
- **VPS** удобен, если хост не хочет держать сервер у себя. Пинг тогда зависит от расположения VPS
  (VPS в Москве для игроков из РФ всё равно лучше EU-региона Photon).

## Что намеренно НЕ делаем

- Не переписываем сетевую часть игры на P2P (Steam Networking или прямые сокеты): это переписывание
  транспорта PUN, его тяжело поддерживать после патчей игры.
- Не пишем свой Photon-сервер, пока LuxonServer справляется.
- Не трогаем античит и проверку владения игрой (см. [03-steam-and-legal.md](03-steam-and-legal.md)).
