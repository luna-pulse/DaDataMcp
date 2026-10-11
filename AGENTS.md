# AGENTS.md

Инструкции для AI-агентов, работающих с этим репозиторием.

## Что это

Локальный MCP-сервер на C# / .NET 8 поверх API подсказок [DaData](https://dadata.ru/). Транспорт — только **stdio**: Cursor запускает собранный DLL как дочерний процесс и говорит с ним JSON-RPC через stdin/stdout. HTTP-порта нет.

Три tool (все read-only, идут в `https://suggestions.dadata.ru`, бесплатный лимит 10 000 запросов в день):

| Tool | Эндпоинт DaData | Вход |
| --- | --- | --- |
| `get_country` | `suggest/country` | `query` (≤ 300 символов), `count` 1–20 |
| `get_address` | `geolocate/address` | `lat`, `lon`, `count` 1–20, `radiusMeters` 1–1000 |
| `find_address_by_ip` | `iplocate/address` | `ip` (IPv4/IPv6) |

Платные методы DaData (cleaner, стандартизация, ближайшее метро) не используются и не добавляются.

## Структура

```text
src/DadataMcp.Server/
  Program.cs                      хост, ServerInfo/ServerInstructions, регистрация tools, DI
  Tools/DaDataTools.cs            три tool: валидация, вызов клиента, маппинг, логирование
  Client/DaDataClient.cs          HTTP к DaData через Flurl, маппинг HTTP-ошибок
  Client/DaDataClientException.cs ошибка с HTTP-статусом
  Configuration/ApiKeyGate.cs     форма ввода ключа (MCP elicitation, mode: form)
  Configuration/ApiKeySession.cs  ключ в памяти, TTL и продление; ApiKeySessionOptions
  Logging/ToolCallLog.cs          строка лога вызова tool в stderr
  Models/ToolResults.cs           контракт ответа tools (сериализуется в camelCase JSON)
  Models/Api/*.cs                 DTO ответов DaData
  logo.png                        иконка сервера, embedded resource
.cursor/mcp.json                  рабочий конфиг Cursor (без секретов)
.cursor/mcp.json.example          пример запуска через dotnet exec
docs/verification/queries.md      проверочные запросы для ДЗ
```

Тестового проекта нет.

## Сборка и запуск

```bash
dotnet restore
dotnet build
```

Cursor запускает `dotnet exec src/DadataMcp.Server/bin/Debug/net8.0/DadataMcp.Server.dll` (см. `.cursor/mcp.json`), поэтому сборка обязательна до включения MCP.

Пока сервер включён в Cursor, процесс держит DLL, и `dotnet build` падает с ошибкой блокировки файла. Перед пересборкой выключите сервер в **Settings → MCP**, после сборки включите снова.

Ручная проверка handshake: запустить DLL и отправить в stdin одну строку:

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"smoke","version":"0.0.1"}}}
```

В ответе `serverInfo.name` = `dadata-mcp`, `version` = `1.0.0`.

## Жёсткие правила

### stdout принадлежит протоколу

- Никогда не пишите в `Console.Out` / `Console.WriteLine`: любой лишний байт в stdout ломает JSON-RPC и роняет MCP.
- Все логи — только в stderr: `Console.Error.WriteLine` или `ILogger` (консольный провайдер настроен на stderr в `Program.cs`).
- Не меняйте кодировку UTF-8 без BOM для stdin/stdout.

### API-ключ

- Ключ живёт **только в памяти процесса** (`ApiKeySession`). Не добавляйте `.env`, переменные окружения, файлы, user secrets или параметры `mcp.json` для ключа.
- Единственный источник ключа — форма в чате через `ApiKeyGate.EnsureApiKeyAsync` (elicitation). Ключ не входит в схему аргументов tools и не возвращается в результатах.
- Ключ не логируется ни в каком виде, в том числе в заголовках и телах запросов.
- Заголовок `Authorization: Token …` берётся из сессии на каждый запрос, а не кешируется в клиенте.
- На `401` от DaData сессия сбрасывается (`DaDataClient.Wrap`), следующий вызов снова откроет форму.
- Через переменные окружения или аргументы запуска можно менять только сроки: `DADATA_SESSION_TTL_MINUTES` (по умолчанию 1440), `DADATA_SESSION_RENEW_MINUTES` (60), `DADATA_FORM_TIMEOUT_SECONDS` (180).

### Область доступа tools

Tools не читают файлы, не запускают процессы ОС и не ходят никуда, кроме `suggestions.dadata.ru`.

## Как добавить или изменить tool

1. Метод в `DaDataTools` с атрибутами `[McpServerTool(Name = "snake_case", Title = ..., ReadOnly = true, Destructive = false, Idempotent = true)]` и `[Description(...)]` на русском. В описании — когда вызывать tool и фраза «Не запрашивай API-ключ в чате.».
2. Первым параметром — `McpServer server`, последним — `CancellationToken`. У каждого входного параметра — `[Description]` с примером значения.
3. Внутри `try` первым делом `await EnsureApiKeyAsync(server, cancellationToken)`, затем валидация входа.
4. На каждый исход ровно один вызов `ToolCallLog.Write(toolName, parameters, "success" | "error", httpStatus?)`. Ловите `DaDataClientException` отдельно, чтобы передать HTTP-статус, затем общий `Exception`; оба `throw;` после логирования.
5. Новый эндпоинт — метод в `IDaDataClient` / `DaDataClient` через общий `PostAsync<T>`; тело запроса в snake_case, как ждёт DaData.
6. DTO ответа DaData — в `Models/Api/`, результат tool — в `Models/ToolResults.cs`. Наружу отдаётся типизированный объект, а не строка. Пустой результат — пустой массив или `null` плюс человекочитаемое `Message`.
7. Если tool новый — обновите `ServerInstructions` в `Program.cs`, раздел «Tool outputs contract» в `README.md` и `docs/verification/queries.md`.

## Стиль кода

- `Nullable` и `ImplicitUsings` включены; file-scoped namespaces; классы `sealed`, если не нужен наследник.
- Приватные поля `_camelCase`, зависимости через конструктор, сервисы регистрируются singleton в `Program.cs`.
- Тексты для пользователя, описания tools, XML-комментарии и сообщения логов — на русском.
- Комментарии только там, где код сам не объясняет ограничение (почему stderr, почему ключ не кешируется и т. п.).

## Документация

- `README.md` — основной документ сдачи ДЗ. В нём есть ссылки на конкретные строки кода (`L21–L76` и т. п.): если сдвигаете код в `DaDataTools.cs`, `DaDataClient.cs`, `Program.cs` или `ToolCallLog.cs`, обновите номера строк.
- `REPORT.md` — история создания проекта, дописывается пунктами в конец.
- `.cursor/*.plan.md` — рабочие планы, не являются документацией.

## Известные расхождения README и кода

- README и `docs/verification/queries.md` показывают строки лога `dadata_request=` / `dadata_response`, но `DaDataClient` их сейчас не пишет; в stderr есть только `params=… status=…` из `ToolCallLog` и строки `[api-key] …`.
- `docs/verification/queries.md` говорит, что форма открывается при включении MCP; на самом деле только при первом вызове tool.

Перед тем как опираться на эти разделы README, сверяйтесь с кодом.
