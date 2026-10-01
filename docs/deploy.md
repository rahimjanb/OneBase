# Выкладка OneBase на сервер

Схема: Ubuntu + Docker, сайт открывается через **Cloudflare Tunnel** — HTTPS даёт Cloudflare, порты сервера наружу не открываются
(`cloudflared → nginx → web / api`). База переносится с рабочего компьютера дампом: пользователи, роли, настройки AI и Linko
(ключи зашифрованы, ключ шифрования лежит в той же базе), загруженные данные Linko и чаты.

Что нужно на сервере: Docker с `docker compose` (v2), `git`, `openssl`, `curl`; от 4 ГБ памяти (сборка фронтенда) и от 15 ГБ диска.

## 1. На рабочем компьютере

**Сменить пароль администратора.** Сейчас вход `1` / `1`, а база уйдёт в интернет. «Настройки → Пользователи и роли» →
«Изменить» в своей строке → «Новый пароль» (от 8 символов) → «Сохранить». Логин тоже лучше сменить на нормальный.

**Сделать дамп базы** (PowerShell):

```powershell
docker exec onebase-postgres-1 pg_dump -U onebase -d onebase -Fc -f /tmp/onebase.dump
docker cp onebase-postgres-1:/tmp/onebase.dump $HOME\onebase.dump
docker exec onebase-postgres-1 rm /tmp/onebase.dump
```

**Скопировать дамп на сервер** (подставьте пользователя и IP):

```powershell
scp $HOME\onebase.dump root@IP_СЕРВЕРА:~/
```

В дампе персональные данные и зашифрованные ключи — после выкладки удалите его и с компьютера, и с сервера.

## 2. Cloudflare Tunnel

1. Cloudflare → **Zero Trust** → **Networks → Tunnels** → **Create a tunnel** → **Cloudflared**, имя `onebase`.
2. На шаге установки скопируйте **токен** — длинную строку после `--token` в показанной команде. Саму команду запускать
   не нужно: cloudflared запустится в Docker.
3. **Public hostname**: домен (и поддомен, если нужен) → **Service**: тип `HTTP`, адрес `nginx:80` → **Save**.

## 3. На сервере

```bash
git clone https://github.com/rahimjanb/OneBase.git ~/onebase
cd ~/onebase

bash deploy/setup-env.sh                  # спросит домен и токен туннеля; пароли сгенерирует сам
bash deploy/restore-db.sh ~/onebase.dump  # только в пустую базу, до первого запуска
docker compose up -d --build              # первая сборка — 5–10 минут

curl -s http://127.0.0.1:8080/health      # ответ: Healthy
docker compose ps                         # все сервисы Up, cloudflared тоже
rm ~/onebase.dump
```

Откройте `https://ваш-домен` и войдите.

## 4. После запуска

- «Настройки → Интеграции → Linko» — «Проверить»; синхронизация идёт сама каждые 20 минут.
- Остановите OneBase на рабочем компьютере, чтобы Linko не синхронизировался дважды.
- Резервные копии: `bash deploy/backup.sh` — дамп в `~/onebase-backups`, хранятся последние 14. Каждую ночь — строка для
  `crontab -e` есть в начале скрипта. Копии лучше периодически забирать с сервера.

## Перенос только данных Linko

Если на сервере уже свои пользователи и настройки, а данных Linko нет (или полная синхронизация идёт слишком долго),
переносятся только схемы `linko` и `sales` — вместе с состоянием синхронизации, так что дальше Linko догружается
с того же места. Пользователи, роли, ключи AI, токены Linko и чаты сервера не трогаются.

На рабочем компьютере (PowerShell):

```powershell
docker exec onebase-postgres-1 pg_dump -U onebase -d onebase -Fc --data-only --schema=linko --schema=sales -f /tmp/onebase-linko.dump
docker cp onebase-postgres-1:/tmp/onebase-linko.dump $HOME\onebase-linko.dump
docker exec onebase-postgres-1 rm /tmp/onebase-linko.dump
scp $HOME\onebase-linko.dump root@IP_СЕРВЕРА:~/
```

На сервере:

```bash
cd ~/onebase && git pull
bash deploy/restore-linko.sh ~/onebase-linko.dump
rm ~/onebase-linko.dump
```

Скрипт останавливает API на время переноса и запускает снова. Всё одной транзакцией: при ошибке данные сервера
остаются как были.

## Обновление

```bash
cd ~/onebase && bash deploy/update.sh
```

Скрипт забирает новую версию из GitHub, пересобирает `api` и `web`, перезапускает и ждёт ответа `/health`.
Миграции базы применяются при старте API. Данные (тома Docker) не затрагиваются.

## Если что-то не так

| Симптом | Что смотреть |
|---|---|
| Сайт не открывается | `docker compose logs --tail 50 cloudflared` — токен туннеля, Public hostname → `nginx:80` |
| 502 / ошибка сервера | `docker compose logs --tail 100 api` и `docker compose logs --tail 100 web` |
| «This page couldn't load», в логе web — `does not match origin` | В `.env` нет `PUBLIC_HOST=ваш-домен`; добавить и `docker compose up -d` |
| «Неверный логин или пароль» у администратора | `bash deploy/reset-admin.sh ЛОГИН` — новый пароль (вводится скрыто), вход включается, роль «Администратор»; логина нет — создаётся. Покажет и число заказов Linko: 0 — дамп не восстановлен |
| В журнале «Linko …: нет ответа за 60 с» | Скорость ответа Linko с сервера: `docker compose exec -T api dotnet OneBase.Api.dll linko-page stock_transfers 1000 200 2>&1 \| grep страница`. Если долго — уменьшить размер страницы этой сущности в `Linko:PageSizes` (appsettings.json) |
| После входа снова страница входа | Сайт открыт по http — сессия ставится только по https. nginx перенаправляет сам, если задан `PUBLIC_HOST` |
| Консультант обрывается | `docker compose logs api` — таймауты; nginx держит поток до 300 с, API шлёт «пульс» каждые 15 с |

Файл `.env` на сервере — секреты (`chmod 600`); в git он не попадает. Храните его копию в надёжном месте.
Пароль PostgreSQL из `.env` задаётся базе при первом запуске: если потом поменять его только в `.env`, API не подключится
к базе — менять нужно и в самой базе (`ALTER USER`).
