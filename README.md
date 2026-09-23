# DaData Local MCP

Локальный MCP-сервер на C# / .NET 8. Отдаёт агенту в Cursor три инструмента по API [DaData](https://dadata.ru/): страна по названию или коду, адрес по координатам, город по IP. Это собственное решение в репозитории, не облачный MCP с сайта DaData.

## Как IDE подключается к MCP и что такое tool

Cursor запускает этот сервер как дочерний процесс и говорит с ним по **stdio**: JSON-RPC идёт через stdin/stdout, логи — только в stderr. При подключении IDE выполняет handshake (`initialize`), забирает метаданные сервера (`dadata-mcp` 1.0.0) и список tools. Дальше агент в чате может вызвать tool по имени: передаёт аргументы по JSON-схеме, сервер ходит в DaData и возвращает структурированный JSON.

**Tool** здесь — метод с атрибутом `[McpServerTool]`: понятное имя, описание, схема входных параметров и объектный результат. Три tool: `get_country`, `get_address`, `find_address_by_ip`. Ключи DaData не входят в схему tool и не спрашиваются в чате.

## Требования

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Аккаунт [DaData](https://dadata.ru/) с **API-ключом**

Все три метода идут в [подсказки](https://dadata.ru/api/suggest/country/) (`suggestions.dadata.ru`): до 10 000 запросов в день бесплатно. Платный cleaner / стандартизация адреса не используется.

## Сборка

```bash
dotnet restore
dotnet build
```

Сборка обязательна до включения MCP: Cursor запускает уже собранный DLL через `dotnet exec`.

## Как включить в Cursor

1. Соберите проект: `dotnet build`.
2. Скопируйте [`.env.example`](.env.example) в `.env` в корне проекта и заполните `DADATA_API_KEY` и `DADATA_SECRET_KEY`. Файл `.env` в git не попадает.
3. Пример конфига MCP без ключей: [`.cursor/mcp.json.example`](.cursor/mcp.json.example). Рабочий файл: [`.cursor/mcp.json`](.cursor/mcp.json).
4. Cursor → **Settings → MCP**. Включите сервер `dadata-local`. При старте процесс читает `.env` из корня проекта.
5. В чате агента проверьте, что видны tools `get_country`, `get_address`, `find_address_by_ip`. Проверочные запросы: [`docs/verification/queries.md`](docs/verification/queries.md).

Если ключей нет, процесс завершится с ошибкой в stderr: нужно заполнить `.env`.

## Безопасность

- Секреты лежат только в `.env` в корне проекта. Файла нет в git (см. `.gitignore`). В исходниках, в `mcp.json` и в аргументах tools ключей нет.
- Tools не читают файлы диска и не запускают команды ОС. Область доступа — только HTTP к `suggestions.dadata.ru`.
- Логи пишут имя tool, входные параметры и `success`/`error`, без ключей.

## Tool outputs contract

Контракт результата задан типами в [`src/DadataMcp.Server/Models/ToolResults.cs`](src/DadataMcp.Server/Models/ToolResults.cs) (L1–L43). MCP сериализует объекты в JSON (camelCase), это не «просто текст».

### `get_country`

Вход: `query` (string, до 300 символов); опционально `count` (1–20, по умолчанию 10). Поиск по названию, цифровому коду, альфа-2 и альфа-3 ([документация](https://dadata.ru/api/suggest/country/)).

```json
{
  "suggestions": [
    {
      "value": "Таиланд",
      "code": "764",
      "alfa2": "TH",
      "alfa3": "THA",
      "nameShort": "Таиланд",
      "name": "Королевство Таиланд"
    }
  ],
  "message": null
}
```

Если совпадений нет: `suggestions` — пустой массив, в `message` пояснение.

### `get_address`

Вход: `lat`, `lon`; опционально `count` (1–20, по умолчанию 10), `radiusMeters` (1–1000, по умолчанию 100).

```json
{
  "suggestions": [
    {
      "value": "г Москва, ул Сухонская, д 11",
      "unrestrictedValue": "127642, г Москва, ул Сухонская, д 11",
      "postalCode": "127642",
      "country": "Россия",
      "region": "г Москва",
      "city": null,
      "street": "ул Сухонская",
      "house": "11",
      "geoLat": "55.878",
      "geoLon": "37.653",
      "fiasId": "..."
    }
  ]
}
```

### `find_address_by_ip`

Вход: `ip` (IPv4 или IPv6).

```json
{
  "location": {
    "value": "г Краснодар",
    "unrestrictedValue": "350000, Краснодарский край, г Краснодар",
    "postalCode": "350000",
    "city": "г Краснодар"
  },
  "message": null
}
```

Если город не определён: `{ "location": null, "message": "Город по указанному IP определить не удалось." }`.

## Подтверждения ссылками на код

### MCP-сервер

Подъём хоста, метаданные и регистрация tools: [`src/DadataMcp.Server/Program.cs`](src/DadataMcp.Server/Program.cs) **L31–L47**.

Клиент DaData: [`src/DadataMcp.Server/Client/DaDataClient.cs`](src/DadataMcp.Server/Client/DaDataClient.cs) **L20–L81**.

### Инструменты

| Tool | Реализация | Логи вызова |
| --- | --- | --- |
| `get_country` | [`DaDataTools.cs`](src/DadataMcp.Server/Tools/DaDataTools.cs) **L21–L76** | L38, L44, L50, L59, L68, L73 |
| `get_address` | [`DaDataTools.cs`](src/DadataMcp.Server/Tools/DaDataTools.cs) **L78–L126** | L98, L104, L113, L118, L123 |
| `find_address_by_ip` | [`DaDataTools.cs`](src/DadataMcp.Server/Tools/DaDataTools.cs) **L128–L173** | L142, L149, L157, L165, L170 |

Общий вывод в stderr: [`src/DadataMcp.Server/Logging/ToolCallLog.cs`](src/DadataMcp.Server/Logging/ToolCallLog.cs) **L12–L20**.

Пример вывода:

```text
[get_country] params={"query":"та","count":10} status=success
[get_address] params={"lat":55.878,"lon":37.653,"count":10,"radiusMeters":100} status=success
[find_address_by_ip] params={"ip":"46.226.227.20"} status=success
[get_country] params={"query":"...","count":10} status=error http=401
```

### Агент вызывает нужный tool

Пример запроса: «Какая страна по запросу та?»

- ожидаемый tool: `get_country`
- фактическое подтверждение: лог stderr в формате выше; скриншот/trace после живого прогона — [`docs/verification/`](docs/verification/)

## Структура

```text
src/DadataMcp.Server/     MCP-хост, клиент DaData, tools
.env                      ключи DaData, не коммитится
.env.example              образец .env без значений
.cursor/mcp.json          конфиг Cursor без секретов
.cursor/mcp.json.example  тот же пример для отчёта
docs/verification/        шаблон проверочных запросов
```
