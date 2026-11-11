-- Отдельная роль PostgreSQL только на чтение для «Аналитика» (OneBase Intelligence).
--
-- Зачем: аналитический движок и так выполняет каждый запрос в транзакции READ ONLY, со statement_timeout и только по типизированному
-- плану (SQL не пишет модель). Отдельная роль — второй рубеж: даже при ошибке в коде она физически не может изменить данные,
-- а секретные таблицы (сессии, ключи, токены, переписка) ей не видны совсем.
--
-- Запуск (от владельца таблиц OneBase — того же пользователя, под которым работают миграции):
--   psql "$DATABASE_URL" -v ro_password='<новый пароль>' -f deploy/analytik-readonly.sql
-- Затем в .env API (ничего другого не меняя):
--   ConnectionStrings__AnalytikReadOnly=Host=...;Port=5432;Database=<та же база>;Username=onebase_analytik_ro;Password=<тот же пароль>
-- и перезапустить API. Без этой строки Аналитик работает через основное подключение (тоже READ ONLY транзакции).
--
-- Скрипт идемпотентный: повторный запуск обновляет пароль и права, данные не трогает. Новые таблицы, созданные владельцем после
-- запуска, получают SELECT автоматически (ALTER DEFAULT PRIVILEGES); после новых секретных таблиц — запустить скрипт ещё раз.

\set ON_ERROR_STOP on

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'onebase_analytik_ro') THEN
        CREATE ROLE onebase_analytik_ro LOGIN;
    END IF;
END $$;

ALTER ROLE onebase_analytik_ro WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT CONNECTION LIMIT 10
    PASSWORD :'ro_password';
-- Даже если приложение забудет READ ONLY — сервер не даст писать; долгие запросы обрываются.
ALTER ROLE onebase_analytik_ro SET default_transaction_read_only = on;
ALTER ROLE onebase_analytik_ro SET statement_timeout = '30s';
ALTER ROLE onebase_analytik_ro SET lock_timeout = '2s';
ALTER ROLE onebase_analytik_ro SET idle_in_transaction_session_timeout = '60s';

SELECT format('GRANT CONNECT ON DATABASE %I TO onebase_analytik_ro', current_database()) \gexec
REVOKE CREATE, TEMPORARY ON DATABASE :"DBNAME" FROM onebase_analytik_ro;

-- Чтение всех прикладных схем (системные pg_* и information_schema — по умолчанию).
DO $$
DECLARE
    s text;
BEGIN
    FOR s IN
        SELECT nspname FROM pg_namespace
        WHERE nspname NOT LIKE 'pg\_%' AND nspname <> 'information_schema'
    LOOP
        EXECUTE format('GRANT USAGE ON SCHEMA %I TO onebase_analytik_ro', s);
        EXECUTE format('REVOKE CREATE ON SCHEMA %I FROM onebase_analytik_ro', s);
        EXECUTE format('GRANT SELECT ON ALL TABLES IN SCHEMA %I TO onebase_analytik_ro', s);
        EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT SELECT ON TABLES TO onebase_analytik_ro', s);
    END LOOP;
END $$;

-- Закрытые таблицы — те же, что закрыты в каталоге Аналитика (CatalogRules.ClosedTables): секреты, сессии, файлы, переписка, личные
-- контакты кандидатов. Таблицы, которых в этой базе нет, пропускаются.
DO $$
DECLARE
    t text;
    closed text[] := ARRAY[
        'public.__EFMigrationsHistory', 'public.DataProtectionKeys', 'public.UserSessions', 'public.FileLocks', 'public.FileConnections',
        'public.FileUserLinks', 'public.Integrations', 'public.DataSourceConnections', 'public.Files', 'public.Folders', 'public.FileVersions',
        'public.ResourcePermissions',
        'ai.Providers', 'ai.ClaudeSwitchBackup', 'ai.Conversations', 'ai.Messages', 'ai.Memory', 'ai.DataConversations', 'ai.DataMessages',
        'ai.DailyPlans', 'ai.Reports', 'ai.KnowledgeDocuments', 'ai.KnowledgeChunks', 'ai.DialogAgentReplies',
        'telegram.Accounts', 'telegram.LinkCodes', 'telegram.ConversationMessages', 'telegram.Conversations', 'telegram.IncomingMessages',
        'telegram.OutgoingMessages', 'telegram.Chats', 'telegram.Materials', 'telegram.MaterialChunks', 'telegram.UserPermissions',
        'telegram.AgentExecutions', 'telegram.CampaignAttachments',
        'recruitment.HhConnection', 'recruitment.CandidateContacts', 'recruitment.CandidateResumes', 'recruitment.CandidateConsents',
        'recruitment.DialogReads', 'recruitment.Leads',
        'work.PushKeys', 'work.PushSubscriptions', 'work.Notifications', 'work.CalendarEvents', 'work.CalendarEventParticipants',
        'sales.UserScopes'
    ];
    parts text[];
BEGIN
    FOREACH t IN ARRAY closed LOOP
        parts := string_to_array(t, '.');
        IF to_regclass(format('%I.%I', parts[1], parts[2])) IS NOT NULL THEN
            EXECUTE format('REVOKE ALL ON TABLE %I.%I FROM onebase_analytik_ro', parts[1], parts[2]);
        END IF;
    END LOOP;
END $$;

-- Секретные колонки открытых таблиц (хэши паролей, токены, ключи) — без права чтения. Права на колонки в PostgreSQL работают
-- только без права на всю таблицу, поэтому у таких таблиц SELECT выдаётся по колонкам: все, кроме секретных.
DO $$
DECLARE
    r record;
    cols text;
BEGIN
    FOR r IN
        SELECT DISTINCT n.nspname AS s, c.relname AS t
        FROM pg_attribute a
        JOIN pg_class c ON c.oid = a.attrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind IN ('r', 'p', 'v', 'm') AND a.attnum > 0 AND NOT a.attisdropped
          AND n.nspname NOT LIKE 'pg\_%' AND n.nspname <> 'information_schema'
          AND a.attname ~ '(^Protected|PasswordHash$|^Password$|Secret$|SecretHash$|TokenHash$|^Token$|AccessToken$|RefreshToken$|PrivateKey$|AccessHash$|CodeHash$|StateHash$|ApiKey$)'
          AND has_table_privilege('onebase_analytik_ro', c.oid, 'SELECT')
    LOOP
        SELECT string_agg(format('%I', a.attname), ', ' ORDER BY a.attnum) INTO cols
        FROM pg_attribute a
        JOIN pg_class c ON c.oid = a.attrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = r.s AND c.relname = r.t AND a.attnum > 0 AND NOT a.attisdropped
          AND a.attname !~ '(^Protected|PasswordHash$|^Password$|Secret$|SecretHash$|TokenHash$|^Token$|AccessToken$|RefreshToken$|PrivateKey$|AccessHash$|CodeHash$|StateHash$|ApiKey$)';
        EXECUTE format('REVOKE ALL ON TABLE %I.%I FROM onebase_analytik_ro', r.s, r.t);
        IF cols IS NOT NULL THEN
            EXECUTE format('GRANT SELECT (%s) ON TABLE %I.%I TO onebase_analytik_ro', cols, r.s, r.t);
        END IF;
    END LOOP;
END $$;

-- Проверка: роль не может писать ни в одну таблицу, видит прикладные таблицы и не видит закрытые.
SELECT
    (SELECT count(*) FROM pg_tables WHERE schemaname NOT LIKE 'pg\_%' AND schemaname <> 'information_schema'
        AND has_table_privilege('onebase_analytik_ro', format('%I.%I', schemaname, tablename), 'INSERT,UPDATE,DELETE,TRUNCATE')) AS writable_tables,
    (SELECT count(*) FROM pg_tables WHERE schemaname NOT LIKE 'pg\_%' AND schemaname <> 'information_schema'
        AND has_table_privilege('onebase_analytik_ro', format('%I.%I', schemaname, tablename), 'SELECT')) AS readable_tables,
    (SELECT count(*) FROM pg_tables WHERE schemaname NOT LIKE 'pg\_%' AND schemaname <> 'information_schema'
        AND NOT has_table_privilege('onebase_analytik_ro', format('%I.%I', schemaname, tablename), 'SELECT')) AS closed_or_column_tables;
