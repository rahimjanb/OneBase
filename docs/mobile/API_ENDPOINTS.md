# OneBase API — полный каталог эндпоинтов

Сгенерировано скриптом из `backend/src/OneBase.Api/Controllers/*.cs` (атрибуты `[Route]`, `[Http*]`, `[HasPermission]`) 19.11.2026.
Колонка «Право»: код из `Permissions` (`sales.read` = `SalesRead`), `auth` — любой вошедший пользователь (доступ дальше проверяет сервис),
`anon` — без входа, `?` — авторизация задана базовым классом или схемой (Field — `FieldControllerBase` с `[Authorize]`, OneCInternal — ключ службы).

Всего: **853** эндпоинтов в **57** контроллерах.

## Agents (2)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/agents` | auth | List |
| POST | `/api/agents/director/tasks` | AgentsRun | Submit |

## AiAgents (5)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/ai/agents` | AgentsRun | List |
| GET | `/api/ai/agents/settings` | AiSettingsManage | AllSettings |
| PUT | `/api/ai/agents/{code}` | AiSettingsManage | Save |
| POST | `/api/ai/agents/{code}/execute` | AgentsRun | Execute |
| GET | `/api/ai/agents/{code}/settings` | AiSettingsManage | Settings |

## AiAlerts (2)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/ai/alerts` | AgentsRun | List |
| POST | `/api/ai/alerts/run` | AiSettingsManage | Run |

## AiKnowledge (8)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/ai/knowledge/documents` | AiSettingsManage | Documents |
| POST | `/api/ai/knowledge/documents` | AiSettingsManage | Upload |
| DELETE | `/api/ai/knowledge/documents/{id:guid}` | AiSettingsManage | Archive |
| PUT | `/api/ai/knowledge/documents/{id:guid}` | AiSettingsManage | Update |
| GET | `/api/ai/knowledge/documents/{id:guid}/content` | FilesRead | Content |
| GET | `/api/ai/knowledge/documents/{id:guid}/file` | FilesRead | Download |
| POST | `/api/ai/knowledge/reindex` | AiSettingsManage | Reindex |
| GET | `/api/ai/knowledge/search` | FilesRead | Search |

## AiRouting (2)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/ai/routing` | AiSettingsManage | Get |
| POST | `/api/ai/routing/evaluate` | AiSettingsManage | Evaluate |

## AiSettings (9)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/ai/models` | AiSettingsManage | Models |
| PUT | `/api/ai/models/{id:guid}` | AiSettingsManage | SaveModel |
| GET | `/api/ai/providers` | AiSettingsManage | Providers |
| PUT | `/api/ai/providers/{code}` | AiSettingsManage | SaveProvider |
| POST | `/api/ai/providers/{code}/models/refresh` | AiSettingsManage | RefreshModels |
| POST | `/api/ai/providers/{code}/test` | AiSettingsManage | TestProvider |
| GET | `/api/ai/settings` | AiSettingsManage | Settings |
| PUT | `/api/ai/settings` | AiSettingsManage | SaveSettings |
| POST | `/api/ai/settings/test` | AiSettingsManage | TestModel |

## AiTools (2)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/ai/tools` | AiSettingsManage | List |
| POST | `/api/ai/tools/{name}/run` | AiSettingsManage | Run |

## AiUsage (2)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/ai/logs` | AiSettingsManage | Logs |
| GET | `/api/ai/usage` | AiSettingsManage | Usage |

## Analytik (12)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/analytik/catalog` | AnalytikUse | Catalog |
| PUT | `/api/analytik/catalog/annotations` | AnalytikManage | Annotate |
| GET | `/api/analytik/catalog/graph` | AnalytikUse | Graph |
| GET | `/api/analytik/catalog/metrics` | AnalytikUse | Metrics |
| POST | `/api/analytik/catalog/refresh` | AnalytikManage | Refresh |
| GET | `/api/analytik/catalog/runs` | AnalytikManage | Runs |
| GET | `/api/analytik/catalog/table/{schema}/{name}` | AnalytikUse | Table |
| GET | `/api/analytik/entity` | AnalytikUse | Entity |
| GET | `/api/analytik/profile` | AnalytikUse | Profile |
| GET | `/api/analytik/queries` | AnalytikUse | Queries |
| POST | `/api/analytik/query` | AnalytikUse | Query |
| GET | `/api/analytik/status` | AnalytikUse | Status |

## AnalytikExplore (6)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/analytik/explore/business` | AnalytikUse | Business |
| GET | `/api/analytik/explore/capabilities` | AnalytikUse | Capabilities |
| GET | `/api/analytik/explore/compatibility` | AnalytikUse | Compatibility |
| POST | `/api/analytik/explore/export` | AnalytikUse | Export |
| GET | `/api/analytik/explore/passports` | AnalytikUse | Passports |
| POST | `/api/analytik/explore/query` | AnalytikUse | Query |

## AnalytikInsights (25)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| POST | `/api/analytik/chat` | AnalytikUse | Chat |
| GET | `/api/analytik/chat/status` | AnalytikUse | Status |
| GET | `/api/analytik/conversations` | AnalytikUse | Conversations |
| DELETE | `/api/analytik/conversations/{id:guid}` | AnalytikUse | Archive |
| GET | `/api/analytik/conversations/{id:guid}` | AnalytikUse | Conversation |
| PATCH | `/api/analytik/conversations/{id:guid}` | AnalytikUse | Rename |
| GET | `/api/analytik/dashboard` | AnalytikUse | Dashboard |
| GET | `/api/analytik/finance` | AnalytikUse | Finance |
| GET | `/api/analytik/forecast` | AnalytikUse | Forecast |
| GET | `/api/analytik/forecast/backtest` | AnalytikUse | ForecastBacktest |
| GET | `/api/analytik/hr` | AnalytikUse | Hr |
| GET | `/api/analytik/jobs` | AnalytikManage | Jobs |
| POST | `/api/analytik/jobs/{name}/run` | AnalytikManage | RunJob |
| POST | `/api/analytik/messages/{id:guid}/feedback` | AnalytikUse | Feedback |
| GET | `/api/analytik/nav` | AnalytikUse | Nav |
| GET | `/api/analytik/production` | AnalytikUse | Production |
| GET | `/api/analytik/readiness` | AnalytikUse | Readiness |
| POST | `/api/analytik/readiness/{source}/refresh` | AnalytikUse | Refresh |
| GET | `/api/analytik/reports` | AnalytikUse | Reports |
| POST | `/api/analytik/reports` | AnalytikUse | CreateReport |
| GET | `/api/analytik/reports/{id:guid}` | AnalytikUse | Report |
| GET | `/api/analytik/reports/{id:guid}/{format}/{*fileName}` | AnalytikUse | ReportFile |
| GET | `/api/analytik/risks` | AnalytikUse | Risks |
| GET | `/api/analytik/sales` | AnalytikUse | Sales |
| GET | `/api/analytik/supply` | AnalytikUse | Supply |

## AnalytikIntelligence (19)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/analytik/benchmark` | AnalytikManage | Benchmark |
| POST | `/api/analytik/benchmark/live` | AnalytikManage | BenchmarkLive |
| POST | `/api/analytik/benchmark/regression` | AnalytikManage | BenchmarkRegression |
| GET | `/api/analytik/benchmark/runs/{id:guid}` | AnalytikManage | BenchmarkRun |
| GET | `/api/analytik/decisions` | AnalytikUse | Decisions |
| POST | `/api/analytik/decisions` | AnalytikUse | CreateDecision |
| GET | `/api/analytik/decisions/{id:guid}` | AnalytikUse | Decision |
| POST | `/api/analytik/decisions/{id:guid}/cancel` | AnalytikUse | CancelDecision |
| POST | `/api/analytik/decisions/{id:guid}/evaluate` | AnalytikUse | EvaluateDecision |
| GET | `/api/analytik/insights/{id:guid}/history` | AnalytikUse | History |
| GET | `/api/analytik/investigations` | AnalytikUse | Investigations |
| GET | `/api/analytik/investigations/{id:guid}` | AnalytikUse | Investigation |
| GET | `/api/analytik/quality` | AnalytikManage | Quality |
| GET | `/api/analytik/quality/explain` | AnalytikUse | Explain |
| POST | `/api/analytik/research` | AnalytikUse | Research |
| GET | `/api/analytik/research/kinds` | AnalytikUse | ResearchKindsList |
| POST | `/api/analytik/research/match` | AnalytikUse | ResearchMatch |
| POST | `/api/analytik/research/{id:guid}/cancel` | AnalytikUse | CancelResearch |
| POST | `/api/analytik/statistics` | AnalytikUse | Statistics |

## AnalytikSimulator (18)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| POST | `/api/analytik/simulator/ai` | AnalytikUse | AiDraft |
| POST | `/api/analytik/simulator/analyze` | AnalytikUse | Analyze |
| POST | `/api/analytik/simulator/baseline` | AnalytikUse | Baseline |
| GET | `/api/analytik/simulator/baseline/{id:guid}` | AnalytikUse | BaselineById |
| GET | `/api/analytik/simulator/catalog` | AnalytikUse | Catalog |
| GET | `/api/analytik/simulator/compare` | AnalytikUse | Compare |
| POST | `/api/analytik/simulator/run` | AnalytikUse | Run |
| GET | `/api/analytik/simulator/scenarios` | AnalytikUse | List |
| POST | `/api/analytik/simulator/scenarios` | AnalytikUse | Create |
| GET | `/api/analytik/simulator/scenarios/{id:guid}` | AnalytikUse | Get |
| PUT | `/api/analytik/simulator/scenarios/{id:guid}` | AnalytikUse | Update |
| POST | `/api/analytik/simulator/scenarios/{id:guid}/ai` | AnalytikUse | Ai |
| POST | `/api/analytik/simulator/scenarios/{id:guid}/archive` | AnalytikUse | Archive |
| POST | `/api/analytik/simulator/scenarios/{id:guid}/copy` | AnalytikUse | Copy |
| GET | `/api/analytik/simulator/scenarios/{id:guid}/export/{format}` | AnalytikUse | Export |
| POST | `/api/analytik/simulator/scenarios/{id:guid}/recalculate` | AnalytikUse | Recalculate |
| POST | `/api/analytik/simulator/scenarios/{id:guid}/restore` | AnalytikUse | Restore |
| POST | `/api/analytik/simulator/scenarios/{id:guid}/set` | AnalytikUse | Set |

## Approvals (3)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/approvals` | ApprovalsDecide | List |
| POST | `/api/approvals/{id:guid}/approve` | ApprovalsDecide | Approve |
| POST | `/api/approvals/{id:guid}/reject` | ApprovalsDecide | Reject |

## Audit (1)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/audit` | AuditRead | List |

## Auth (4)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| POST | `/api/auth/login` | anon | Login |
| POST | `/api/auth/logout` | auth | Logout |
| GET | `/api/auth/me` | auth | Me |
| POST | `/api/auth/whats-new` | auth | WhatsNewSeen |

## Calendar (7)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/calendar` | auth | Items |
| GET | `/api/calendar/abilities` | auth | Abilities |
| POST | `/api/calendar/events` | auth | Create |
| DELETE | `/api/calendar/events/{id:guid}` | auth | Delete |
| GET | `/api/calendar/events/{id:guid}` | auth | Get |
| PUT | `/api/calendar/events/{id:guid}` | auth | Update |
| GET | `/api/calendar/people` | auth | People |

## Consultant (12)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| POST | `/api/ai/chat` | AgentsRun | Chat |
| GET | `/api/ai/conversations` | AgentsRun | Conversations |
| DELETE | `/api/ai/conversations/{id:guid}` | AgentsRun | Archive |
| GET | `/api/ai/conversations/{id:guid}` | AgentsRun | Conversation |
| PATCH | `/api/ai/conversations/{id:guid}` | AgentsRun | Rename |
| POST | `/api/ai/messages/{id:guid}/feedback` | AgentsRun | Feedback |
| GET | `/api/ai/messages/{id:guid}/speech` | AgentsRun | Speech |
| GET | `/api/ai/messages/{id:guid}/speech-text` | AgentsRun | SpeechParts |
| GET | `/api/ai/reports/{id:guid}` | AgentsRun | Report |
| GET | `/api/ai/reports/{id:guid}/xlsx/{*fileName}` | AgentsRun | ReportXlsx |
| GET | `/api/ai/status` | AgentsRun | Status |
| POST | `/api/ai/voice/transcribe` | AgentsRun | Transcribe |

## DataManager (14)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/data-manager/change-sets` | DataManage | ChangeSets |
| GET | `/api/data-manager/change-sets/{id:guid}` | DataManage | ChangeSet |
| POST | `/api/data-manager/change-sets/{id:guid}/apply` | DataManage | Apply |
| POST | `/api/data-manager/change-sets/{id:guid}/cancel` | DataManage | Cancel |
| POST | `/api/data-manager/change-sets/{id:guid}/rollback` | DataManage | Rollback |
| POST | `/api/data-manager/change-sets/{id:guid}/verify` | DataManage | Verify |
| POST | `/api/data-manager/chat` | DataManage | Chat |
| GET | `/api/data-manager/conversations` | DataManage | Conversations |
| POST | `/api/data-manager/conversations` | DataManage | Create |
| DELETE | `/api/data-manager/conversations/{id:guid}` | DataManage | Archive |
| GET | `/api/data-manager/conversations/{id:guid}` | DataManage | Conversation |
| PATCH | `/api/data-manager/conversations/{id:guid}` | DataManage | Rename |
| POST | `/api/data-manager/files` | DataManage | Upload |
| GET | `/api/data-manager/status` | DataManage | Status |

## DataSources (17)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/settings/data-sources` | IntegrationsManage | Overview |
| POST | `/api/settings/data-sources/auto-map` | IntegrationsManage | AutoMap |
| POST | `/api/settings/data-sources/auto-map/confirm` | IntegrationsManage | ConfirmAutoMap |
| POST | `/api/settings/data-sources/bindings` | IntegrationsManage | CreateBinding |
| DELETE | `/api/settings/data-sources/bindings/{id:guid}` | IntegrationsManage | DeleteBinding |
| PUT | `/api/settings/data-sources/bindings/{id:guid}` | IntegrationsManage | UpdateBinding |
| GET | `/api/settings/data-sources/bindings/{id:guid}/runs` | IntegrationsManage | Runs |
| POST | `/api/settings/data-sources/bindings/{id:guid}/sync` | IntegrationsManage | Sync |
| GET | `/api/settings/data-sources/conflicts` | IntegrationsManage | Conflicts |
| POST | `/api/settings/data-sources/conflicts/{id:guid}/close` | IntegrationsManage | CloseConflict |
| POST | `/api/settings/data-sources/connections` | IntegrationsManage | CreateConnection |
| DELETE | `/api/settings/data-sources/connections/{id:guid}` | IntegrationsManage | DeleteConnection |
| PUT | `/api/settings/data-sources/connections/{id:guid}` | IntegrationsManage | UpdateConnection |
| POST | `/api/settings/data-sources/connections/{id:guid}/test` | IntegrationsManage | TestConnection |
| GET | `/api/settings/data-sources/export` | IntegrationsManage | Export |
| POST | `/api/settings/data-sources/import` | IntegrationsManage | Import |
| POST | `/api/settings/data-sources/preview` | IntegrationsManage | Preview |

## Departments (3)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/settings/departments` | auth | List |
| GET | `/api/settings/departments/users` | auth | Users |
| PUT | `/api/settings/departments/{code}/heads` | auth | SetHeads |

## DialogAgents (20)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| POST | `/api/agents` | auth | Create |
| GET | `/api/agents/departments` | auth | Departments |
| POST | `/api/agents/replies/{replyId:guid}/approve` | auth | Approve |
| POST | `/api/agents/replies/{replyId:guid}/rate` | auth | Rate |
| POST | `/api/agents/replies/{replyId:guid}/reject` | auth | Reject |
| DELETE | `/api/agents/{id:guid}` | auth | Delete |
| GET | `/api/agents/{id:guid}` | auth | Get |
| PUT | `/api/agents/{id:guid}` | auth | Save |
| PUT | `/api/agents/{id:guid}/account` | auth | SetAccount |
| POST | `/api/agents/{id:guid}/answers` | auth | AddAnswer |
| DELETE | `/api/agents/{id:guid}/answers/{answerId:guid}` | auth | DeleteAnswer |
| PUT | `/api/agents/{id:guid}/answers/{answerId:guid}` | auth | UpdateAnswer |
| GET | `/api/agents/{id:guid}/conversations` | auth | Conversations |
| GET | `/api/agents/{id:guid}/conversations/{conversationId:guid}` | auth | Conversation |
| POST | `/api/agents/{id:guid}/conversations/{conversationId:guid}/messages` | auth | Send |
| PUT | `/api/agents/{id:guid}/conversations/{conversationId:guid}/mode` | auth | Mode |
| POST | `/api/agents/{id:guid}/default` | auth | MakeDefault |
| GET | `/api/agents/{id:guid}/health` | auth | Health |
| GET | `/api/agents/{id:guid}/replies` | auth | Replies |
| POST | `/api/agents/{id:guid}/test` | auth | Test |

## Field (19)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/field/agents` | ? | Agents |
| GET | `/api/field/agents/{id:guid}` | ? | Agent |
| GET | `/api/field/audit` | ? | Audit |
| GET | `/api/field/dashboard` | ? | Dashboard |
| GET | `/api/field/kpi` | ? | Kpi |
| GET | `/api/field/me` | ? | Me |
| POST | `/api/field/members` | ? | CreateMember |
| PUT | `/api/field/members/{id:guid}` | ? | UpdateMember |
| POST | `/api/field/members/{id:guid}/access` | ? | GrantAccess |
| GET | `/api/field/notifications` | ? | Notifications |
| POST | `/api/field/notifications/read-all` | ? | ReadAll |
| POST | `/api/field/notifications/{id:guid}/read` | ? | Read |
| GET | `/api/field/settings` | ? | GetSettings |
| PUT | `/api/field/settings` | ? | PutSettings |
| GET | `/api/field/structure` | ? | Structure |
| POST | `/api/field/structure/import` | ? | Import |
| POST | `/api/field/teams` | ? | CreateTeam |
| PUT | `/api/field/teams/{id:guid}` | ? | UpdateTeam |
| GET | `/api/field/today` | ? | Today |

## FieldWork (32)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/field/ai/recommendations` | ? | List |
| POST | `/api/field/ai/recommendations/generate` | ? | Generate |
| POST | `/api/field/ai/recommendations/{id:guid}/approve` | ? | Approve |
| POST | `/api/field/ai/recommendations/{id:guid}/reject` | ? | Reject |
| GET | `/api/field/customers` | ? | List |
| GET | `/api/field/customers/{id:long}` | ? | Get |
| PUT | `/api/field/customers/{id:long}` | ? | Update |
| POST | `/api/field/customers/{id:long}/agent` | ? | ChangeAgent |
| GET | `/api/field/map` | ? | Map |
| GET | `/api/field/routes` | ? | List |
| POST | `/api/field/routes/build` | ? | Build |
| GET | `/api/field/routes/for` | ? | For |
| POST | `/api/field/routes/points/{pointId:guid}/skip` | ? | Skip |
| GET | `/api/field/routes/{id:guid}` | ? | Get |
| POST | `/api/field/routes/{id:guid}/optimize` | ? | Optimize |
| PUT | `/api/field/routes/{id:guid}/points` | ? | Points |
| GET | `/api/field/tasks` | ? | List |
| POST | `/api/field/tasks` | ? | Create |
| GET | `/api/field/tasks/{id:guid}` | ? | Get |
| PUT | `/api/field/tasks/{id:guid}` | ? | Update |
| POST | `/api/field/tasks/{id:guid}/status` | ? | Status |
| GET | `/api/field/visits` | ? | List |
| GET | `/api/field/visits/active` | ? | Active |
| GET | `/api/field/visits/joint` | ? | Joint |
| POST | `/api/field/visits/joint` | ? | CreateJoint |
| PUT | `/api/field/visits/joint/{id:guid}` | ? | UpdateJoint |
| POST | `/api/field/visits/start` | ? | Start |
| GET | `/api/field/visits/{id:guid}` | ? | Get |
| POST | `/api/field/visits/{id:guid}/cancel` | ? | Cancel |
| POST | `/api/field/visits/{id:guid}/finish` | ? | Finish |
| GET | `/api/field/visits/{id:guid}/photo` | ? | GetPhoto |
| POST | `/api/field/visits/{id:guid}/photo` | ? | Photo |

## Files (43)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/files/departments` | FilesRead | Departments |
| GET | `/api/files/departments/{code}` | FilesRead | Department |
| GET | `/api/files/departments/{code}/access` | FilesRead | DepartmentAccess |
| PUT | `/api/files/departments/{code}/access` | FilesRead | SetDepartmentAccess |
| GET | `/api/files/departments/{code}/connection` | FilesRead | Connection |
| POST | `/api/files/departments/{code}/connection` | FilesRead | CreateConnection |
| POST | `/api/files/departments/{code}/connection/link` | FilesRead | ConnectionLink |
| POST | `/api/files/departments/{code}/connection/link/regenerate` | FilesRead | RegenerateConnectionLink |
| POST | `/api/files/departments/{code}/connection/password` | FilesRead | ConnectionPassword |
| POST | `/api/files/departments/{code}/connection/regenerate` | FilesRead | RegenerateConnection |
| POST | `/api/files/departments/{code}/connection/revoke` | FilesRead | RevokeConnection |
| POST | `/api/files/departments/{code}/connection/test` | FilesRead | TestConnection |
| GET | `/api/files/departments/{code}/trash` | FilesRead | Trash |
| POST | `/api/files/folders` | FilesRead | CreateFolder |
| DELETE | `/api/files/folders/{id:guid}` | FilesRead | DeleteFolder |
| GET | `/api/files/folders/{id:guid}` | FilesRead | Folder |
| PUT | `/api/files/folders/{id:guid}` | FilesRead | RenameFolder |
| POST | `/api/files/folders/{id:guid}/move` | FilesRead | MoveFolder |
| POST | `/api/files/folders/{id:guid}/restore` | FilesRead | RestoreFolder |
| POST | `/api/files/folders/{id:guid}/upload` | FilesRead | Upload |
| GET | `/api/files/me/link` | FilesRead | MyLink |
| POST | `/api/files/me/link` | FilesRead | IssueMyLink |
| POST | `/api/files/me/link/reveal` | FilesRead | RevealMyLink |
| POST | `/api/files/me/link/revoke` | FilesRead | RevokeMyLink |
| GET | `/api/files/search` | FilesRead | Search |
| GET | `/api/files/settings` | FilesRead | Settings |
| GET | `/api/files/shared` | FilesRead | SharedFolders |
| POST | `/api/files/shared` | FilesRead | CreateShared |
| DELETE | `/api/files/shared/{id:guid}` | FilesRead | DeleteShared |
| GET | `/api/files/shared/{id:guid}` | FilesRead | Shared |
| PUT | `/api/files/shared/{id:guid}` | FilesRead | RenameShared |
| GET | `/api/files/shared/{id:guid}/access` | FilesRead | SharedAccess |
| PUT | `/api/files/shared/{id:guid}/access` | FilesRead | SetSharedAccess |
| GET | `/api/files/shared/{id:guid}/trash` | FilesRead | SharedTrash |
| DELETE | `/api/files/{id:guid}` | FilesRead | Delete |
| PUT | `/api/files/{id:guid}` | FilesRead | Rename |
| GET | `/api/files/{id:guid}/download` | FilesRead | Download |
| GET | `/api/files/{id:guid}/download/{*name}` | FilesRead | Download |
| POST | `/api/files/{id:guid}/move` | FilesRead | Move |
| GET | `/api/files/{id:guid}/preview` | FilesRead | Preview |
| POST | `/api/files/{id:guid}/restore` | FilesRead | Restore |
| POST | `/api/files/{id:guid}/unlock` | FilesRead | Unlock |
| GET | `/api/files/{id:guid}/versions` | FilesRead | Versions |

## Finance (11)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/finance/accounts` | FinanceRead | Accounts |
| GET | `/api/finance/accounts/{code}` | FinanceRead | Account |
| GET | `/api/finance/anomalies` | FinanceRead | Anomalies |
| GET | `/api/finance/cash` | FinanceRead | Cash |
| GET | `/api/finance/documents/{id:guid}` | FinanceRead | Document |
| GET | `/api/finance/entries` | FinanceRead | Entries |
| GET | `/api/finance/operations` | FinanceRead | Operations |
| GET | `/api/finance/organizations` | FinanceRead | Organizations |
| GET | `/api/finance/overview` | FinanceRead | Overview |
| GET | `/api/finance/pnl` | FinanceRead | Pnl |
| GET | `/api/finance/settlements` | FinanceRead | Settlements |

## Hr (12)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/hr/attendance` | auth | Attendance |
| GET | `/api/hr/context` | auth | Context |
| GET | `/api/hr/deviations` | auth | Deviations |
| GET | `/api/hr/employees` | auth | Employees |
| GET | `/api/hr/employees/{id:guid}` | auth | Employee |
| PUT | `/api/hr/employees/{id:guid}/note` | auth | Note |
| GET | `/api/hr/events` | auth | Events |
| GET | `/api/hr/leaders` | auth | Leaders |
| GET | `/api/hr/overview` | auth | Overview |
| GET | `/api/hr/reports` | auth | Report |
| GET | `/api/hr/reports/export` | auth | Export |
| GET | `/api/hr/timesheet` | auth | Timesheet |

## Integrations (7)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/integrations` | IntegrationsManage | Departments |
| GET | `/api/integrations/departments/{code}` | IntegrationsManage | Department |
| GET | `/api/integrations/linko` | IntegrationsManage | Linko |
| PUT | `/api/integrations/linko` | IntegrationsManage | SaveLinko |
| POST | `/api/integrations/linko/reset` | IntegrationsManage | ResetLinko |
| POST | `/api/integrations/linko/test` | IntegrationsManage | TestLinko |
| POST | `/api/integrations/linko/verify` | IntegrationsManage | VerifyLinko |

## Logs (1)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/logs` | LogsRead | List |

## Me (6)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/me/ai-plan` | auth | AiPlan |
| POST | `/api/me/ai-plan/refresh` | auth | RefreshAiPlan |
| GET | `/api/me/calendar` | auth | Calendar |
| GET | `/api/me/documents` | auth | Documents |
| GET | `/api/me/tasks` | auth | Tasks |
| GET | `/api/me/workday` | auth | Workday |

## OneC (17)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/integrations/onec` | IntegrationsManage | Status |
| GET | `/api/integrations/onec/changes` | IntegrationsManage | Changes |
| POST | `/api/integrations/onec/clear` | IntegrationsManage | Clear |
| GET | `/api/integrations/onec/failures` | IntegrationsManage | Failures |
| POST | `/api/integrations/onec/failures/retry` | IntegrationsManage | Retry |
| POST | `/api/integrations/onec/failures/{id:guid}/ignore` | IntegrationsManage | Ignore |
| POST | `/api/integrations/onec/historical` | IntegrationsManage | Historical |
| POST | `/api/integrations/onec/historical/cancel` | IntegrationsManage | CancelHistorical |
| POST | `/api/integrations/onec/pause` | IntegrationsManage | Pause |
| POST | `/api/integrations/onec/resume` | IntegrationsManage | Resume |
| GET | `/api/integrations/onec/runs` | IntegrationsManage | Runs |
| GET | `/api/integrations/onec/runs/{id:guid}` | IntegrationsManage | Run |
| PUT | `/api/integrations/onec/settings` | IntegrationsManage | Save |
| POST | `/api/integrations/onec/sync` | IntegrationsManage | Sync |
| POST | `/api/integrations/onec/test` | IntegrationsManage | Test |
| POST | `/api/integrations/onec/test-sync` | IntegrationsManage | TestSync |
| POST | `/api/integrations/onec/token` | IntegrationsManage | Token |

## OneCInternal (12)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| POST | `/api/internal/onec/batches` | ? | Batch |
| POST | `/api/internal/onec/commands/{id:guid}/complete` | ? | Complete |
| POST | `/api/internal/onec/failures` | ? | Failure |
| GET | `/api/internal/onec/failures/due` | ? | DueFailures |
| POST | `/api/internal/onec/failures/{id:guid}/outcome` | ? | Outcome |
| POST | `/api/internal/onec/heartbeat` | ? | Heartbeat |
| GET | `/api/internal/onec/historical/{id:guid}` | ? | Historical |
| POST | `/api/internal/onec/historical/{id:guid}/progress` | ? | HistoricalProgress |
| POST | `/api/internal/onec/runs` | ? | Start |
| POST | `/api/internal/onec/runs/{id:guid}/finish` | ? | Finish |
| POST | `/api/internal/onec/runs/{id:guid}/progress` | ? | Progress |
| GET | `/api/internal/onec/totals` | ? | Totals |

## Procurement (56)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| DELETE | `/api/procurement/attachments/{id:guid}` | auth | DeleteAttachment |
| GET | `/api/procurement/attachments/{id:guid}` | auth | Download |
| GET | `/api/procurement/batches` | auth | Batches |
| GET | `/api/procurement/batches/options` | auth | BatchOptions |
| GET | `/api/procurement/batches/{id:guid}` | auth | Batch |
| GET | `/api/procurement/catalog` | auth | Catalog |
| GET | `/api/procurement/categories` | auth | Categories |
| POST | `/api/procurement/categories` | auth | CreateCategory |
| PUT | `/api/procurement/categories/{id:guid}` | auth | UpdateCategory |
| GET | `/api/procurement/form` | auth | Form |
| POST | `/api/procurement/items` | auth | CreateItem |
| POST | `/api/procurement/items/import` | auth | ImportCatalog |
| PUT | `/api/procurement/items/minimums` | auth | SetMinimums |
| GET | `/api/procurement/items/{id:guid}` | auth | Item |
| PUT | `/api/procurement/items/{id:guid}` | auth | UpdateItem |
| GET | `/api/procurement/items/{id:guid}/prices` | auth | ItemPrices |
| GET | `/api/procurement/movements` | auth | Movements |
| GET | `/api/procurement/nav` | auth | Nav |
| GET | `/api/procurement/overview` | auth | Overview |
| GET | `/api/procurement/requests` | auth | List |
| POST | `/api/procurement/requests` | auth | Create |
| GET | `/api/procurement/requests/{id:guid}` | auth | Get |
| PUT | `/api/procurement/requests/{id:guid}` | auth | Update |
| POST | `/api/procurement/requests/{id:guid}/approve` | auth | Approve |
| POST | `/api/procurement/requests/{id:guid}/attachments` | auth | Attach |
| POST | `/api/procurement/requests/{id:guid}/cancel` | auth | Cancel |
| POST | `/api/procurement/requests/{id:guid}/forward` | auth | Forward |
| POST | `/api/procurement/requests/{id:guid}/issue` | auth | IssueForRequest |
| POST | `/api/procurement/requests/{id:guid}/reject` | auth | Reject |
| POST | `/api/procurement/requests/{id:guid}/return` | auth | Return |
| POST | `/api/procurement/requests/{id:guid}/submit` | auth | Submit |
| GET | `/api/procurement/settings` | auth | Settings |
| PUT | `/api/procurement/settings` | auth | UpdateSettings |
| GET | `/api/procurement/stock` | auth | Stock |
| POST | `/api/procurement/stock/batch-entry` | auth | BatchEntry |
| GET | `/api/procurement/stock/departments` | auth | StockDepartments |
| POST | `/api/procurement/stock/documents/{id:guid}/reverse` | auth | Reverse |
| POST | `/api/procurement/stock/import` | auth | ImportStock |
| POST | `/api/procurement/stock/inventory` | auth | Inventory |
| POST | `/api/procurement/stock/issue` | auth | Issue |
| GET | `/api/procurement/stock/minimums` | auth | Minimums |
| POST | `/api/procurement/stock/receipt` | auth | Receipt |
| GET | `/api/procurement/stock/sheets` | auth | SheetImports |
| POST | `/api/procurement/stock/sheets` | auth | CreateSheetImport |
| DELETE | `/api/procurement/stock/sheets/{id:guid}` | auth | DeleteSheetImport |
| PUT | `/api/procurement/stock/sheets/{id:guid}` | auth | UpdateSheetImport |
| POST | `/api/procurement/stock/sheets/{id:guid}/run` | auth | RunSheetImport |
| GET | `/api/procurement/stock/summary` | auth | StockSummary |
| POST | `/api/procurement/stock/supplier-return` | auth | SupplierReturn |
| GET | `/api/procurement/stock/to-issue` | auth | ToIssue |
| POST | `/api/procurement/stock/transfer` | auth | Transfer |
| POST | `/api/procurement/stock/write-off` | auth | WriteOff |
| GET | `/api/procurement/stock/xlsx` | auth | StockExcel |
| GET | `/api/procurement/warehouses` | auth | Warehouses |
| POST | `/api/procurement/warehouses` | auth | CreateWarehouse |
| PUT | `/api/procurement/warehouses/{id:guid}` | auth | UpdateWarehouse |

## ProcurementAnalytics (10)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/procurement/board` | auth | Board |
| GET | `/api/procurement/dashboard` | auth | Dashboard |
| GET | `/api/procurement/digests` | auth | Digests |
| GET | `/api/procurement/digests/{id:guid}` | auth | Digest |
| GET | `/api/procurement/receipts/{id:guid}/act` | auth | ReceiptAct |
| GET | `/api/procurement/reports` | auth | ReportKinds |
| GET | `/api/procurement/reports/{kind}` | auth | Report |
| GET | `/api/procurement/reports/{kind}/pdf` | auth | ReportPdf |
| GET | `/api/procurement/reports/{kind}/xlsx` | auth | ReportExcel |
| GET | `/api/procurement/requests/{id:guid}/docx` | auth | RequestDocument |

## ProcurementPurchasing (53)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/procurement/arrivals` | auth | Arrivals |
| POST | `/api/procurement/arrivals` | auth | RegisterArrival |
| GET | `/api/procurement/arrivals/expected` | auth | ExpectedDeliveries |
| GET | `/api/procurement/arrivals/{id:guid}` | auth | Arrival |
| POST | `/api/procurement/arrivals/{id:guid}/accept` | auth | AcceptArrival |
| POST | `/api/procurement/arrivals/{id:guid}/cancel` | auth | CancelArrival |
| POST | `/api/procurement/claims/{id:guid}/resolve` | auth | ResolveClaim |
| GET | `/api/procurement/documents` | auth | Documents |
| POST | `/api/procurement/documents` | auth | Attach |
| GET | `/api/procurement/home` | auth | Home |
| PUT | `/api/procurement/offers/{id:guid}` | auth | UpdateOffer |
| POST | `/api/procurement/offers/{id:guid}/reject` | auth | RejectOffer |
| POST | `/api/procurement/offers/{id:guid}/restore` | auth | RestoreOffer |
| GET | `/api/procurement/orders` | auth | Orders |
| POST | `/api/procurement/orders` | auth | CreateOrder |
| GET | `/api/procurement/orders/needs` | auth | Needs |
| GET | `/api/procurement/orders/responsibles` | auth | Responsibles |
| GET | `/api/procurement/orders/summary` | auth | OrdersSummary |
| GET | `/api/procurement/orders/{id:guid}` | auth | Order |
| PUT | `/api/procurement/orders/{id:guid}` | auth | UpdateOrder |
| POST | `/api/procurement/orders/{id:guid}/approve` | auth | ApproveOrder |
| POST | `/api/procurement/orders/{id:guid}/cancel` | auth | CancelOrder |
| POST | `/api/procurement/orders/{id:guid}/close-remainder` | auth | CloseRemainder |
| POST | `/api/procurement/orders/{id:guid}/complete` | auth | CompleteOrder |
| POST | `/api/procurement/orders/{id:guid}/ordered` | auth | MarkOrdered |
| POST | `/api/procurement/orders/{id:guid}/reject` | auth | RejectOrder |
| POST | `/api/procurement/orders/{id:guid}/return` | auth | ReturnOrder |
| POST | `/api/procurement/orders/{id:guid}/submit` | auth | SubmitOrder |
| GET | `/api/procurement/receipts` | auth | Receipts |
| POST | `/api/procurement/receipts` | auth | CreateReceipt |
| GET | `/api/procurement/receipts/expected` | auth | ExpectedReceipts |
| GET | `/api/procurement/receipts/form` | auth | ReceiptForm |
| DELETE | `/api/procurement/receipts/{id:guid}` | auth | DeleteReceipt |
| GET | `/api/procurement/receipts/{id:guid}` | auth | Receipt |
| PUT | `/api/procurement/receipts/{id:guid}` | auth | UpdateReceipt |
| POST | `/api/procurement/receipts/{id:guid}/post` | auth | PostReceipt |
| GET | `/api/procurement/requests/{id:guid}/offers` | auth | Offers |
| POST | `/api/procurement/requests/{id:guid}/offers` | auth | CreateOffer |
| POST | `/api/procurement/requests/{id:guid}/purchase` | auth | Purchase |
| GET | `/api/procurement/suppliers` | auth | Suppliers |
| POST | `/api/procurement/suppliers` | auth | CreateSupplier |
| POST | `/api/procurement/suppliers/import` | auth | ImportSuppliers |
| GET | `/api/procurement/suppliers/options` | auth | SupplierOptions |
| GET | `/api/procurement/suppliers/{id:guid}` | auth | Supplier |
| PUT | `/api/procurement/suppliers/{id:guid}` | auth | UpdateSupplier |
| GET | `/api/procurement/tenders` | auth | Tenders |
| POST | `/api/procurement/tenders` | auth | CreateTender |
| GET | `/api/procurement/tenders/{id:guid}` | auth | Tender |
| PUT | `/api/procurement/tenders/{id:guid}` | auth | UpdateTender |
| POST | `/api/procurement/tenders/{id:guid}/cancel` | auth | CancelTender |
| POST | `/api/procurement/tenders/{id:guid}/delivered` | auth | TenderDelivered |
| POST | `/api/procurement/tenders/{id:guid}/delivery` | auth | TenderDelivery |
| GET | `/api/procurement/to-buy` | auth | ToBuy |

## Production (55)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/production/nav` | auth | Nav |
| GET | `/api/production/orders` | auth | Orders |
| POST | `/api/production/orders` | auth | CreateOrder |
| GET | `/api/production/orders/form` | auth | OrderForm |
| GET | `/api/production/orders/{id:guid}` | auth | Order |
| PUT | `/api/production/orders/{id:guid}` | auth | UpdateOrder |
| POST | `/api/production/orders/{id:guid}/cancel` | auth | Cancel |
| POST | `/api/production/orders/{id:guid}/complete` | auth | Complete |
| POST | `/api/production/orders/{id:guid}/issue` | auth | Issue |
| POST | `/api/production/orders/{id:guid}/receive-output` | auth | ReceiveOutput |
| POST | `/api/production/orders/{id:guid}/release` | auth | Release |
| POST | `/api/production/orders/{id:guid}/return` | auth | Return |
| GET | `/api/production/overview` | auth | Overview |
| GET | `/api/production/products` | auth | Products |
| GET | `/api/production/recipe-imports` | auth | RecipeImports |
| POST | `/api/production/recipe-imports` | auth | UploadRecipeImport |
| GET | `/api/production/recipe-imports/{id:guid}` | auth | RecipeImport |
| POST | `/api/production/recipe-imports/{id:guid}/apply` | auth | ApplyRecipeImport |
| PUT | `/api/production/recipe-imports/{id:guid}/lines/{lineId:guid}` | auth | DecideRecipeImportLine |
| POST | `/api/production/recipe-imports/{id:guid}/map` | auth | MapRecipeImportArticle |
| GET | `/api/production/recipe-imports/{id:guid}/missing` | auth | RecipeImportMissing |
| GET | `/api/production/recipe-imports/{id:guid}/recipes/{recipeId:guid}` | auth | RecipeImportRecipe |
| PUT | `/api/production/recipe-imports/{id:guid}/recipes/{recipeId:guid}` | auth | UpdateRecipeImportRecipe |
| POST | `/api/production/recipe-imports/{id:guid}/rematch` | auth | RematchRecipeImport |
| GET | `/api/production/recipe-photos/{fileId:guid}/{width:int}` | auth | RecipePhoto |
| GET | `/api/production/recipes` | auth | Recipes |
| POST | `/api/production/recipes` | auth | CreateRecipe |
| GET | `/api/production/recipes/form` | auth | RecipeForm |
| GET | `/api/production/recipes/options` | auth | RecipeOptions |
| GET | `/api/production/recipes/{id:guid}` | auth | Recipe |
| PUT | `/api/production/recipes/{id:guid}` | auth | UpdateRecipe |
| POST | `/api/production/recipes/{id:guid}/approve` | auth | Approve |
| POST | `/api/production/recipes/{id:guid}/archive` | auth | Archive |
| GET | `/api/production/recipes/{id:guid}/compare/{other:guid}` | auth | Compare |
| POST | `/api/production/recipes/{id:guid}/new-version` | auth | NewVersion |
| DELETE | `/api/production/recipes/{id:guid}/photo` | auth | DeleteRecipePhoto |
| POST | `/api/production/recipes/{id:guid}/photo` | auth | UploadRecipePhoto |
| GET | `/api/production/recipes/{id:guid}/requirement` | auth | Requirement |
| POST | `/api/production/recipes/{id:guid}/return` | auth | ReturnRecipe |
| POST | `/api/production/recipes/{id:guid}/submit` | auth | Submit |
| GET | `/api/production/reports` | auth | ReportKinds |
| GET | `/api/production/reports/{kind}` | auth | Report |
| GET | `/api/production/reports/{kind}/pdf` | auth | ReportPdf |
| GET | `/api/production/reports/{kind}/xlsx` | auth | ReportExcel |
| GET | `/api/production/shifts` | auth | Shifts |
| POST | `/api/production/shifts` | auth | CreateShift |
| GET | `/api/production/shifts/form` | auth | ShiftForm |
| GET | `/api/production/shifts/{id:guid}` | auth | Shift |
| PUT | `/api/production/shifts/{id:guid}` | auth | SaveShift |
| POST | `/api/production/shifts/{id:guid}/approve` | auth | ApproveShift |
| POST | `/api/production/shifts/{id:guid}/cancel` | auth | CancelShift |
| POST | `/api/production/shifts/{id:guid}/close` | auth | CloseShift |
| POST | `/api/production/shifts/{id:guid}/correct` | auth | CorrectShift |
| POST | `/api/production/shifts/{id:guid}/return` | auth | ReturnShift |
| GET | `/api/production/technologist` | auth | Technologist |

## Push (4)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/push/key` | auth | Key |
| DELETE | `/api/push/subscriptions` | auth | Unsubscribe |
| POST | `/api/push/subscriptions` | auth | Subscribe |
| POST | `/api/push/test` | auth | Test |

## Quality (12)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| POST | `/api/quality/batches/{id:guid}/block` | auth | Block |
| POST | `/api/quality/batches/{id:guid}/unblock` | auth | Unblock |
| GET | `/api/quality/checks` | auth | Checks |
| GET | `/api/quality/checks/{id:guid}` | auth | Check |
| POST | `/api/quality/checks/{id:guid}/decide` | auth | Decide |
| PUT | `/api/quality/checks/{id:guid}/documents` | auth | Documents |
| PUT | `/api/quality/checks/{id:guid}/lab` | auth | Lab |
| GET | `/api/quality/nav` | auth | Nav |
| GET | `/api/quality/settings` | auth | Settings |
| PUT | `/api/quality/settings` | auth | UpdateSettings |
| GET | `/api/quality/specs/{itemId:guid}` | auth | Specs |
| PUT | `/api/quality/specs/{itemId:guid}` | auth | UpdateSpecs |

## Recovery (2)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/recovery/check` | anon | Check |
| GET | `/api/recovery/mode` | anon | Mode |

## Recruitment (33)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| POST | `/api/hr/recruitment/applications/{id:guid}/stage` | auth | Stage |
| GET | `/api/hr/recruitment/candidates` | auth | Candidates |
| POST | `/api/hr/recruitment/candidates` | auth | CreateCandidate |
| DELETE | `/api/hr/recruitment/candidates/{id:guid}` | auth | DeleteCandidate |
| GET | `/api/hr/recruitment/candidates/{id:guid}` | auth | Candidate |
| PUT | `/api/hr/recruitment/candidates/{id:guid}` | auth | UpdateCandidate |
| POST | `/api/hr/recruitment/candidates/{id:guid}/applications` | auth | AddApplication |
| POST | `/api/hr/recruitment/candidates/{id:guid}/consents` | auth | RecordConsent |
| POST | `/api/hr/recruitment/candidates/{id:guid}/contacts` | auth | AddContact |
| DELETE | `/api/hr/recruitment/candidates/{id:guid}/contacts/{contactId:guid}` | auth | RemoveContact |
| POST | `/api/hr/recruitment/candidates/{id:guid}/resumes` | auth | UploadResume |
| POST | `/api/hr/recruitment/consents/{id:guid}/withdraw` | auth | WithdrawConsent |
| GET | `/api/hr/recruitment/context` | auth | Context |
| POST | `/api/hr/recruitment/duplicates/{id:guid}/dismiss` | auth | Dismiss |
| POST | `/api/hr/recruitment/duplicates/{id:guid}/merge` | auth | Merge |
| GET | `/api/hr/recruitment/nav` | auth | Nav |
| GET | `/api/hr/recruitment/requests` | auth | Requests |
| POST | `/api/hr/recruitment/requests` | auth | CreateRequest |
| POST | `/api/hr/recruitment/requests/{id:guid}/approve` | auth | ApproveRequest |
| POST | `/api/hr/recruitment/requests/{id:guid}/cancel` | auth | CancelRequest |
| POST | `/api/hr/recruitment/requests/{id:guid}/reject` | auth | RejectRequest |
| DELETE | `/api/hr/recruitment/resumes/{id:guid}` | auth | DeleteResume |
| GET | `/api/hr/recruitment/resumes/{id:guid}` | auth | DownloadResume |
| GET | `/api/hr/recruitment/resumes/{id:guid}/preview` | auth | PreviewResume |
| POST | `/api/hr/recruitment/resumes/{id:guid}/read` | auth | ReadResume |
| POST | `/api/hr/recruitment/resumes/{id:guid}/reprocess` | auth | ReprocessResume |
| GET | `/api/hr/recruitment/settings` | auth | Settings |
| PUT | `/api/hr/recruitment/settings` | auth | SaveSettings |
| GET | `/api/hr/recruitment/vacancies` | auth | Vacancies |
| POST | `/api/hr/recruitment/vacancies` | auth | CreateVacancy |
| GET | `/api/hr/recruitment/vacancies/{id:guid}` | auth | Vacancy |
| PUT | `/api/hr/recruitment/vacancies/{id:guid}` | auth | UpdateVacancy |
| POST | `/api/hr/recruitment/vacancies/{id:guid}/status` | auth | VacancyStatus |

## RecruitmentFlow (26)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/hr/recruitment/ai-settings` | auth | AiSettings |
| PUT | `/api/hr/recruitment/ai-settings` | auth | SaveAiSettings |
| GET | `/api/hr/recruitment/analytics` | auth | Analytics |
| POST | `/api/hr/recruitment/applications/{id:guid}/propose` | auth | Propose |
| POST | `/api/hr/recruitment/applications/{id:guid}/score` | auth | Score |
| GET | `/api/hr/recruitment/candidates/{id:guid}/interviews` | auth | CandidateInterviews |
| GET | `/api/hr/recruitment/candidates/{id:guid}/scores` | auth | Scores |
| GET | `/api/hr/recruitment/exceptions` | auth | Exceptions |
| POST | `/api/hr/recruitment/exceptions/{id:guid}/resolve` | auth | Resolve |
| GET | `/api/hr/recruitment/interviews` | auth | Interviews |
| POST | `/api/hr/recruitment/interviews/book` | auth | Book |
| GET | `/api/hr/recruitment/interviews/bookable` | auth | Bookable |
| GET | `/api/hr/recruitment/interviews/{id:guid}` | auth | Interview |
| POST | `/api/hr/recruitment/interviews/{id:guid}/cancel` | auth | Cancel |
| POST | `/api/hr/recruitment/interviews/{id:guid}/confirm` | auth | Confirm |
| POST | `/api/hr/recruitment/interviews/{id:guid}/feedback` | auth | Feedback |
| POST | `/api/hr/recruitment/interviews/{id:guid}/reschedule` | auth | Reschedule |
| POST | `/api/hr/recruitment/interviews/{id:guid}/status` | auth | InterviewStatus |
| GET | `/api/hr/recruitment/recruiters` | auth | Recruiters |
| PUT | `/api/hr/recruitment/vacancies/{id:guid}/ai` | auth | VacancyAi |
| GET | `/api/hr/recruitment/vacancies/{id:guid}/interview-settings` | auth | InterviewSettings |
| PUT | `/api/hr/recruitment/vacancies/{id:guid}/interview-settings` | auth | SaveInterviewSettings |
| GET | `/api/hr/recruitment/vacancies/{id:guid}/interviewers` | auth | Interviewers |
| PUT | `/api/hr/recruitment/vacancies/{id:guid}/interviewers` | auth | SetInterviewers |
| POST | `/api/hr/recruitment/vacancies/{id:guid}/post-text` | auth | PostText |
| GET | `/api/hr/recruitment/vacancies/{id:guid}/slots` | auth | Slots |

## RecruitmentHh (12)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/hr/recruitment/hh` | auth | Status |
| GET | `/api/hr/recruitment/hh/candidates/{candidateId:guid}/negotiations` | auth | Negotiations |
| POST | `/api/hr/recruitment/hh/complete` | auth | Complete |
| POST | `/api/hr/recruitment/hh/connect` | auth | Connect |
| POST | `/api/hr/recruitment/hh/disconnect` | auth | Disconnect |
| GET | `/api/hr/recruitment/hh/guard` | auth | Guard |
| POST | `/api/hr/recruitment/hh/guard/reset` | auth | ResetGuard |
| POST | `/api/hr/recruitment/hh/link` | auth | Link |
| POST | `/api/hr/recruitment/hh/negotiations/{id:guid}/action` | auth | Action |
| PUT | `/api/hr/recruitment/hh/settings` | auth | Save |
| POST | `/api/hr/recruitment/hh/sync` | auth | Sync |
| GET | `/api/hr/recruitment/hh/vacancies` | auth | Vacancies |

## RecruitmentScreening (11)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/hr/recruitment/screening` | auth | Page |
| POST | `/api/hr/recruitment/screening/applications/{id:guid}/analyze` | auth | Analyze |
| POST | `/api/hr/recruitment/screening/applications/{id:guid}/status` | auth | Status |
| GET | `/api/hr/recruitment/screening/candidates/{id:guid}` | auth | Candidate |
| POST | `/api/hr/recruitment/screening/compare` | auth | Compare |
| GET | `/api/hr/recruitment/screening/comparisons/{id:guid}` | auth | Comparison |
| POST | `/api/hr/recruitment/screening/import/file` | auth | ImportFile |
| POST | `/api/hr/recruitment/screening/import/link` | auth | ImportLink |
| POST | `/api/hr/recruitment/screening/import/text` | auth | ImportText |
| POST | `/api/hr/recruitment/screening/queue` | auth | Queue |
| GET | `/api/hr/recruitment/screening/vacancies/{id:guid}/comparisons` | auth | Comparisons |

## RecruitmentTelegram (23)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/hr/recruitment/telegram/accounts` | auth | Accounts |
| GET | `/api/hr/recruitment/telegram/accounts/{accountId:guid}/chats` | auth | Chats |
| GET | `/api/hr/recruitment/telegram/conversations` | auth | Conversations |
| GET | `/api/hr/recruitment/telegram/conversations/{id:guid}` | auth | Conversation |
| POST | `/api/hr/recruitment/telegram/conversations/{id:guid}/messages` | auth | Send |
| PUT | `/api/hr/recruitment/telegram/conversations/{id:guid}/mode` | auth | Mode |
| GET | `/api/hr/recruitment/telegram/dialogs` | auth | Dialogs |
| POST | `/api/hr/recruitment/telegram/dialogs/ask-admin` | auth | AskAdmin |
| GET | `/api/hr/recruitment/telegram/dialogs/summary` | auth | DialogSummary |
| GET | `/api/hr/recruitment/telegram/dialogs/{id:guid}` | auth | Dialog |
| POST | `/api/hr/recruitment/telegram/dialogs/{id:guid}/draft` | auth | Draft |
| POST | `/api/hr/recruitment/telegram/dialogs/{id:guid}/files` | auth | SendFile |
| POST | `/api/hr/recruitment/telegram/dialogs/{id:guid}/retry/{outgoingId:guid}` | auth | Retry |
| GET | `/api/hr/recruitment/telegram/leads` | auth | Leads |
| POST | `/api/hr/recruitment/telegram/leads/{id:guid}/convert` | auth | ConvertLead |
| POST | `/api/hr/recruitment/telegram/leads/{id:guid}/ignore` | auth | IgnoreLead |
| GET | `/api/hr/recruitment/telegram/publications` | auth | Publications |
| POST | `/api/hr/recruitment/telegram/publications` | auth | Publish |
| POST | `/api/hr/recruitment/telegram/publications/{id:guid}/cancel` | auth | CancelPublication |
| GET | `/api/hr/recruitment/telegram/sources` | auth | Sources |
| POST | `/api/hr/recruitment/telegram/sources` | auth | AddSource |
| DELETE | `/api/hr/recruitment/telegram/sources/{id:guid}` | auth | RemoveSource |
| GET | `/api/hr/recruitment/telegram/vacancies/{vacancyId:guid}/template` | auth | Template |

## Sales (34)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/sales/agents` | SalesRead | Agents |
| GET | `/api/sales/agents/{id:long}` | SalesRead | Agent |
| GET | `/api/sales/agents/{id:long}/day` | SalesRead | AgentDay |
| GET | `/api/sales/akt` | FinanceRead | Akt |
| GET | `/api/sales/assortment` | SalesRead | Assortment |
| GET | `/api/sales/categories/{id}` | SalesRead | Category |
| GET | `/api/sales/directions/{id}` | SalesRead | Direction |
| GET | `/api/sales/dmp` | SalesRead | Dmp |
| GET | `/api/sales/dso` | FinanceRead | Dso |
| GET | `/api/sales/export` | SalesRead | Export |
| GET | `/api/sales/kpi` | SalesRead | Kpi |
| PUT | `/api/sales/kpi/region` | SalesManage | KpiRegion |
| GET | `/api/sales/months` | SalesRead | Months |
| GET | `/api/sales/my` | SalesRead | MyRegions |
| GET | `/api/sales/outstock` | SalesRead | Outstock |
| GET | `/api/sales/overview` | SalesRead | Overview |
| GET | `/api/sales/plans` | SalesRead | Plans |
| GET | `/api/sales/primary` | SalesRead | Primary |
| GET | `/api/sales/primary/export` | SalesRead | PrimaryExport |
| GET | `/api/sales/primary/overview` | SalesRead | PrimaryOverview |
| GET | `/api/sales/primary/overview/xlsx/{fileName}` | SalesExport | PrimaryOverviewWorkbook |
| GET | `/api/sales/problems` | SalesRead | Problems |
| GET | `/api/sales/products/{id:long}` | SalesRead | Product |
| GET | `/api/sales/promo` | SalesRead | Promo |
| GET | `/api/sales/regions/{id:guid}` | SalesRead | Region |
| GET | `/api/sales/republic` | SalesRead | Republic |
| GET | `/api/sales/sku-sales` | SalesRead | SkuSales |
| GET | `/api/sales/stock` | SalesRead | Stock |
| GET | `/api/sales/stock/autoorder` | SalesRead | AutoOrder |
| POST | `/api/sales/stock/autoorder/act/{fileName}` | SalesExport | AutoOrderAct |
| GET | `/api/sales/stock/xlsx/{fileName}` | SalesExport | StockWorkbook |
| GET | `/api/sales/stores/{id:long}` | SalesRead | Store |
| GET | `/api/sales/system` | SalesRead | SystemHealth |
| GET | `/api/sales/visit-sequence` | SalesRead | VisitSequence |

## SalesPnl (2)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/sales/pnl` | SalesRead, FinanceRead | Get |
| GET | `/api/sales/pnl/dealer` | SalesRead, FinanceRead | Dealer |

## SalesProductPhotos (5)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/sales/product-photos` | SalesRead | List |
| GET | `/api/sales/product-photos/{fileId:guid}/{size:int}` | SalesRead | File |
| DELETE | `/api/sales/product-photos/{id:guid}` | SalesManage | Delete |
| POST | `/api/sales/product-photos/{id:guid}/main` | SalesManage | Main |
| POST | `/api/sales/products/{productId:long}/photos` | SalesManage | Upload |

## SalesSetup (9)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/sales/setup` | SalesRead | Get |
| POST | `/api/sales/setup/directions` | SalesManage | CreateDirection |
| DELETE | `/api/sales/setup/directions/{id:guid}` | SalesManage | DeleteDirection |
| PUT | `/api/sales/setup/directions/{id:guid}` | SalesManage | UpdateDirection |
| GET | `/api/sales/setup/factory-plan` | SalesRead | FactoryPlan |
| PUT | `/api/sales/setup/factory-plan` | SalesManage | UpdateFactoryPlan |
| GET | `/api/sales/setup/kpi-plans` | SalesRead | KpiPlans |
| PUT | `/api/sales/setup/regions/{id:guid}` | SalesManage | UpdateRegion |
| PUT | `/api/sales/setup/targets` | SalesManage | UpdateTargets |

## SalesSync (3)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/sales/status` | SalesRead | Status |
| POST | `/api/sales/sync` | SalesManage | Sync |
| POST | `/api/sales/sync/cancel` | SalesManage | Cancel |

## Telegram (33)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/me/telegram` | auth | Links |
| POST | `/api/me/telegram/code` | auth | Code |
| DELETE | `/api/me/telegram/{permissionId:guid}` | auth | Unlink |
| GET | `/api/settings/telegram` | TelegramManage | Overview |
| POST | `/api/settings/telegram/accounts` | TelegramManage | CreateAccount |
| DELETE | `/api/settings/telegram/accounts/{id:guid}` | TelegramManage | DeleteAccount |
| PUT | `/api/settings/telegram/accounts/{id:guid}` | TelegramManage | RenameAccount |
| GET | `/api/settings/telegram/accounts/{id:guid}/access` | TelegramManage | Access |
| PUT | `/api/settings/telegram/accounts/{id:guid}/access` | TelegramManage | SaveAccess |
| GET | `/api/settings/telegram/accounts/{id:guid}/agent` | TelegramManage | Settings |
| PUT | `/api/settings/telegram/accounts/{id:guid}/agent` | TelegramManage | SaveSettings |
| POST | `/api/settings/telegram/accounts/{id:guid}/agent/test` | TelegramManage | TestAgent |
| GET | `/api/settings/telegram/accounts/{id:guid}/chats` | TelegramManage | Chats |
| POST | `/api/settings/telegram/accounts/{id:guid}/chats/sync` | TelegramManage | SyncChats |
| PUT | `/api/settings/telegram/accounts/{id:guid}/chats/{chatId:guid}/agent` | TelegramManage | ChatAgent |
| POST | `/api/settings/telegram/accounts/{id:guid}/check` | TelegramManage | Check |
| GET | `/api/settings/telegram/accounts/{id:guid}/conversations` | TelegramManage | Conversations |
| PUT | `/api/settings/telegram/accounts/{id:guid}/conversations/{peerId:long}/department` | TelegramManage | AssignConversation |
| PUT | `/api/settings/telegram/accounts/{id:guid}/conversations/{peerId:long}/mode` | TelegramManage | ConversationMode |
| GET | `/api/settings/telegram/accounts/{id:guid}/departments` | TelegramManage | Departments |
| PUT | `/api/settings/telegram/accounts/{id:guid}/departments` | TelegramManage | SaveDepartments |
| POST | `/api/settings/telegram/accounts/{id:guid}/disconnect` | TelegramManage | Disconnect |
| GET | `/api/settings/telegram/accounts/{id:guid}/executions` | TelegramManage | Executions |
| POST | `/api/settings/telegram/accounts/{id:guid}/login` | TelegramManage | StartLogin |
| POST | `/api/settings/telegram/accounts/{id:guid}/login/code` | TelegramManage | SubmitCode |
| POST | `/api/settings/telegram/accounts/{id:guid}/login/password` | TelegramManage | SubmitPassword |
| GET | `/api/settings/telegram/accounts/{id:guid}/users` | TelegramManage | Users |
| POST | `/api/settings/telegram/accounts/{id:guid}/users` | TelegramManage | CreatePermission |
| POST | `/api/settings/telegram/accounts/{id:guid}/users/bulk` | TelegramManage | Bulk |
| DELETE | `/api/settings/telegram/accounts/{id:guid}/users/{permissionId:guid}` | TelegramManage | DeletePermission |
| PUT | `/api/settings/telegram/accounts/{id:guid}/users/{permissionId:guid}` | TelegramManage | UpdatePermission |
| PUT | `/api/settings/telegram/credentials` | TelegramManage | SaveCredentials |
| GET | `/api/settings/telegram/onebase-users` | TelegramManage | OneBaseUsers |

## TelegramCampaigns (24)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/telegram/campaigns` | TelegramCampaigns | List |
| POST | `/api/telegram/campaigns` | TelegramCampaigns | Create |
| GET | `/api/telegram/campaigns/accounts` | TelegramCampaigns | Accounts |
| GET | `/api/telegram/campaigns/reports` | TelegramCampaigns | Reports |
| DELETE | `/api/telegram/campaigns/{id:guid}` | TelegramCampaigns | Delete |
| GET | `/api/telegram/campaigns/{id:guid}` | TelegramCampaigns | Get |
| PUT | `/api/telegram/campaigns/{id:guid}` | TelegramCampaigns | Update |
| DELETE | `/api/telegram/campaigns/{id:guid}/attachments/{attachmentId:guid}` | TelegramCampaigns | RemoveAttachment |
| GET | `/api/telegram/campaigns/{id:guid}/attachments/{attachmentId:guid}` | TelegramCampaigns | DownloadAttachment |
| POST | `/api/telegram/campaigns/{id:guid}/cancel` | TelegramCampaigns | Cancel |
| GET | `/api/telegram/campaigns/{id:guid}/chats` | TelegramCampaigns | Chats |
| POST | `/api/telegram/campaigns/{id:guid}/chats/sync` | TelegramCampaigns | SyncChats |
| POST | `/api/telegram/campaigns/{id:guid}/duplicate` | TelegramCampaigns | Duplicate |
| POST | `/api/telegram/campaigns/{id:guid}/files` | TelegramCampaigns | AddFile |
| POST | `/api/telegram/campaigns/{id:guid}/launch` | TelegramCampaigns | Launch |
| POST | `/api/telegram/campaigns/{id:guid}/pause` | TelegramCampaigns | Pause |
| GET | `/api/telegram/campaigns/{id:guid}/preview` | TelegramCampaigns | Preview |
| DELETE | `/api/telegram/campaigns/{id:guid}/recipients` | TelegramCampaigns | ClearRecipients |
| POST | `/api/telegram/campaigns/{id:guid}/recipients` | TelegramCampaigns | AddRecipients |
| DELETE | `/api/telegram/campaigns/{id:guid}/recipients/{recipientId:guid}` | TelegramCampaigns | RemoveRecipient |
| POST | `/api/telegram/campaigns/{id:guid}/reports` | TelegramCampaigns | AddReport |
| POST | `/api/telegram/campaigns/{id:guid}/resume` | TelegramCampaigns | Resume |
| POST | `/api/telegram/campaigns/{id:guid}/retry` | TelegramCampaigns | Retry |
| POST | `/api/telegram/campaigns/{id:guid}/test` | TelegramCampaigns | Test |

## TelegramCategoryCards (3)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/settings/telegram/reports/category-cards` | TelegramManage | Get |
| PUT | `/api/settings/telegram/reports/category-cards` | TelegramManage | SaveDefaults |
| PUT | `/api/settings/telegram/reports/category-cards/recipients/{id:guid}` | TelegramManage | SaveRecipient |

## TelegramReports (32)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/settings/telegram/reports` | TelegramManage | Overview |
| GET | `/api/settings/telegram/reports/daily-plans` | TelegramManage | DailyPlans |
| POST | `/api/settings/telegram/reports/daily-plans/recalculate` | TelegramManage | RecalculateDailyPlans |
| GET | `/api/settings/telegram/reports/deliveries/{id:guid}/files/{index:int}` | TelegramManage | DeliveryFile |
| POST | `/api/settings/telegram/reports/deliveries/{id:guid}/rebuild` | TelegramManage | Rebuild |
| POST | `/api/settings/telegram/reports/deliveries/{id:guid}/retry` | TelegramManage | Retry |
| GET | `/api/settings/telegram/reports/history` | TelegramManage | History |
| GET | `/api/settings/telegram/reports/holidays` | TelegramManage | Holidays |
| POST | `/api/settings/telegram/reports/holidays` | TelegramManage | AddHoliday |
| DELETE | `/api/settings/telegram/reports/holidays/{id:guid}` | TelegramManage | DeleteHoliday |
| GET | `/api/settings/telegram/reports/incentives` | TelegramManage | Incentives |
| POST | `/api/settings/telegram/reports/incentives` | TelegramManage | CreateIncentive |
| PUT | `/api/settings/telegram/reports/incentives/{code}` | TelegramManage | SaveIncentive |
| GET | `/api/settings/telegram/reports/incentives/{code}/payouts` | TelegramManage | Payouts |
| PUT | `/api/settings/telegram/reports/incentives/{code}/payouts` | TelegramManage | SetPayout |
| PUT | `/api/settings/telegram/reports/incentives/{code}/promos` | TelegramManage | SavePromos |
| GET | `/api/settings/telegram/reports/incentives/{code}/sleeping` | TelegramManage | Sleeping |
| POST | `/api/settings/telegram/reports/incentives/{code}/sleeping/refresh` | TelegramManage | RefreshSleeping |
| GET | `/api/settings/telegram/reports/incentives/{code}/standings` | TelegramManage | IncentiveStandings |
| PUT | `/api/settings/telegram/reports/jobs/{code}` | TelegramManage | SaveJob |
| POST | `/api/settings/telegram/reports/preview` | TelegramManage | Preview |
| POST | `/api/settings/telegram/reports/recipients` | TelegramManage | CreateRecipient |
| PUT | `/api/settings/telegram/reports/recipients` | TelegramManage | UpdateRecipients |
| DELETE | `/api/settings/telegram/reports/recipients/{id:guid}` | TelegramManage | DeleteRecipient |
| PUT | `/api/settings/telegram/reports/recipients/{id:guid}` | TelegramManage | UpdateRecipient |
| POST | `/api/settings/telegram/reports/recipients/{id:guid}/send` | TelegramManage | Send |
| PUT | `/api/settings/telegram/reports/settings` | TelegramManage | SaveSettings |
| POST | `/api/settings/telegram/reports/test` | TelegramManage | Test |
| PUT | `/api/settings/telegram/reports/weekly-pdf` | TelegramManage | SaveWeeklyPdf |
| POST | `/api/settings/telegram/reports/weekly/data` | TelegramManage | WeeklyData |
| POST | `/api/settings/telegram/reports/weekly/pdf` | TelegramManage | WeeklyPdf |
| POST | `/api/settings/telegram/reports/weekly/test` | TelegramManage | WeeklyTest |

## Twin (3)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/twin` | auth | Snapshot |
| PUT | `/api/twin/objects/{key}` | auth | Update |
| GET | `/api/twin/options` | auth | Options |

## Users (4)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/users` | UsersManage | List |
| POST | `/api/users` | UsersManage | Create |
| GET | `/api/users/hr-employees` | UsersManage | HrEmployees |
| PUT | `/api/users/{id:guid}` | UsersManage | Update |

## Verifix (9)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/integrations/verifix` | IntegrationsManage | Details |
| PUT | `/api/integrations/verifix` | IntegrationsManage | Save |
| PUT | `/api/integrations/verifix/divisions/{id:long}/department` | IntegrationsManage | Department |
| GET | `/api/integrations/verifix/runs` | IntegrationsManage | Runs |
| GET | `/api/integrations/verifix/runs/{id:guid}` | IntegrationsManage | Run |
| POST | `/api/integrations/verifix/runs/{id:guid}/retry` | IntegrationsManage | Retry |
| POST | `/api/integrations/verifix/sync` | IntegrationsManage | Sync |
| POST | `/api/integrations/verifix/sync/cancel` | IntegrationsManage | Cancel |
| POST | `/api/integrations/verifix/test` | IntegrationsManage | Test |

## Work (42)

| Метод | Путь | Право | Действие |
|---|---|---|---|
| GET | `/api/notifications` | auth | List |
| GET | `/api/notifications/counts` | auth | Counts |
| GET | `/api/notifications/preferences` | auth | Preferences |
| PUT | `/api/notifications/preferences` | auth | SavePreferences |
| POST | `/api/notifications/read-all` | auth | ReadAll |
| DELETE | `/api/notifications/{id:guid}` | auth | Delete |
| POST | `/api/notifications/{id:guid}/action` | auth | Act |
| POST | `/api/notifications/{id:guid}/read` | auth | Read |
| POST | `/api/notifications/{id:guid}/unread` | auth | Unread |
| GET | `/api/work/assignees` | WorkTasks | Assignees |
| DELETE | `/api/work/attachments/{id:guid}` | WorkTasks | DeleteAttachment |
| GET | `/api/work/attachments/{id:guid}` | WorkTasks | Download |
| GET | `/api/work/departments` | WorkTasks | Departments |
| PUT | `/api/work/departments/{code}/heads` | WorkTasks | SetHeads |
| GET | `/api/work/heads` | WorkTasks | Heads |
| GET | `/api/work/nav` | WorkTasks | Nav |
| GET | `/api/work/overview` | WorkTasks | Overview |
| GET | `/api/work/projects` | WorkTasks | Projects |
| POST | `/api/work/projects` | WorkTasks | CreateProject |
| PUT | `/api/work/projects/{id:guid}` | WorkTasks | UpdateProject |
| GET | `/api/work/suggestions` | WorkTasks | Suggestions |
| POST | `/api/work/suggestions/generate` | WorkTasks | Generate |
| PUT | `/api/work/suggestions/{id:guid}` | WorkTasks | EditSuggestion |
| POST | `/api/work/suggestions/{id:guid}/accept` | WorkTasks | Accept |
| POST | `/api/work/suggestions/{id:guid}/reject` | WorkTasks | Reject |
| GET | `/api/work/tasks` | WorkTasks | List |
| POST | `/api/work/tasks` | WorkTasks | Create |
| GET | `/api/work/tasks/{id:guid}` | WorkTasks | Get |
| PUT | `/api/work/tasks/{id:guid}` | WorkTasks | Update |
| POST | `/api/work/tasks/{id:guid}/archive` | WorkTasks | Archive |
| POST | `/api/work/tasks/{id:guid}/attachments` | WorkTasks | Attach |
| POST | `/api/work/tasks/{id:guid}/block` | WorkTasks | Block |
| POST | `/api/work/tasks/{id:guid}/checklist` | WorkTasks | AddChecklist |
| POST | `/api/work/tasks/{id:guid}/checklist/order` | WorkTasks | OrderChecklist |
| DELETE | `/api/work/tasks/{id:guid}/checklist/{itemId:guid}` | WorkTasks | DeleteChecklist |
| PUT | `/api/work/tasks/{id:guid}/checklist/{itemId:guid}` | WorkTasks | UpdateChecklist |
| POST | `/api/work/tasks/{id:guid}/comments` | WorkTasks | Comment |
| GET | `/api/work/tasks/{id:guid}/people` | WorkTasks | People |
| POST | `/api/work/tasks/{id:guid}/progress` | WorkTasks | Progress |
| POST | `/api/work/tasks/{id:guid}/restore` | WorkTasks | Restore |
| POST | `/api/work/tasks/{id:guid}/status` | WorkTasks | Status |
| GET | `/api/work/users` | WorkTasks | Users |
