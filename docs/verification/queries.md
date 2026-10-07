# Проверочные запросы (домашнее задание)

После подключения MCP в Cursor выполните запросы в чате агента. Скриншоты и логи stderr кладите в эту папку (`docs/verification/`).

Ключ в чат текстом не пишите — он вводится только в форме, которую сервер сам открывает (при включении MCP и при вызове tool, если сессии нет или ей больше суток).

## Запросы с вызовом tool (минимум 3)

| # | Запрос пользователя | Ожидаемый tool | Подтверждение |
| --- | --- | --- | --- |
| 1 | Какая страна имеет код «DE»? | `get_country` | лог/скриншот: `docs/verification/` |
| 2 | Какой адрес у координат 55.878, 37.653? | `get_address` | лог/скриншот: `docs/verification/` |
| 3 | Какой город у IP 46.226.227.20? | `find_address_by_ip` | лог/скриншот: `docs/verification/` |
| 4 | Найди страну с кодом TH | `get_country` | лог/скриншот: `docs/verification/` |

Ожидаемые фрагменты лога MCP-сервера (stderr). На вызов, дошедший до DaData, перед `status=` есть тело POST и сырой JSON ответа:

```text
[get_country] dadata_request={"query":"DE","count":10}
[get_country] dadata_response http=200 {"suggestions":[...]}
[get_country] params={"query":"DE","count":10} status=success
[get_address] dadata_request={"lat":55.878,"lon":37.653,"count":10,"radius_meters":100}
[get_address] dadata_response http=200 {"suggestions":[...]}
[get_address] params={"lat":55.878,"lon":37.653,"count":10,"radiusMeters":100} status=success
[find_address_by_ip] dadata_request={"ip":"46.226.227.20"}
[find_address_by_ip] dadata_response http=200 {"location":{...}}
[find_address_by_ip] params={"ip":"46.226.227.20"} status=success
```

## Запрос без tool (для критерия «5 запросов»)

| # | Запрос пользователя | Ожидаемый tool |
| --- | --- | --- |
| 5 | Объясни, как IDE подключается к MCP-серверу по stdio и что такое tool. | нет (ответ из README / общих знаний) |

Итого: 5 запросов, из них минимум 3 с реальным вызовом MCP-tool.
