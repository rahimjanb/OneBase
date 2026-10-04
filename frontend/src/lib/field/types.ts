// Типы ответов API Sales Base (/api/field/*). Имена — как в backend (camelCase).

export type FieldRole = "Rm" | "Supervisor" | "Agent";
export type FieldPriority = "Low" | "Medium" | "High" | "Urgent";
export type FieldCustomerStatus = "Active" | "Problem" | "Inactive";
export type FieldPointStatus = "Planned" | "InProgress" | "Visited" | "Skipped" | "Cancelled";
export type FieldRouteStatus = "Planned" | "InProgress" | "Completed" | "Cancelled";
export type FieldRouteSource = "Manual" | "Linko" | "Ai";
export type FieldVisitStatus = "InProgress" | "Completed" | "Cancelled";
export type FieldVisitResult = "Order" | "Sale" | "Refusal" | "Revisit" | "Closed" | "NoDecisionMaker" | "Other";
export type FieldGeoStatus = "Ok" | "Far" | "NoGps" | "NoTarget";
export type FieldTaskStatus = "New" | "Accepted" | "InProgress" | "Completed" | "Verified" | "Cancelled" | "Postponed";
/** Office — сотрудник офиса из раздела «Задачи» OneBase (без карточки участника). */
export type FieldActorType = "Rm" | "Supervisor" | "Agent" | "Ai" | "System" | "Office";
export type FieldRecommendationKind = "SalesDecline" | "NotVisited" | "LostCustomer" | "AgentBehindPlan" | "SkippedPoint" | "HighPotential" | "Reassign";
export type FieldRecommendationStatus = "Pending" | "Approved" | "Rejected" | "Expired";
export type AgentDayStatus = "NotStarted" | "OnRoute" | "OnVisit" | "Finished" | "Problem";

export type Page<T> = { items: T[]; total: number; page: number; pageSize: number };

export type FieldVisitRow = {
  id: string;
  marketId: number;
  marketName: string;
  address: string | null;
  agentId: string;
  agentName: string;
  startedAt: string;
  endedAt: string | null;
  minutes: number | null;
  status: FieldVisitStatus;
  result: FieldVisitResult | null;
  amount: number | null;
  comment: string | null;
  geoStatus: FieldGeoStatus;
  distanceM: number | null;
  accuracyM: number | null;
  hasPhoto: boolean;
  routeId: string | null;
  jointVisitId: string | null;
};

export type FieldMe = {
  userId: string;
  memberId: string | null;
  name: string;
  role: FieldRole;
  canPlan: boolean;
  canManage: boolean;
  /** Управляет всей организацией: импорт из Linko, настройки, журнал, РМ. */
  canManageOrg: boolean;
  teamId: string | null;
  teamName: string | null;
  supervisorId: string | null;
  supervisorName: string | null;
  unreadNotifications: number;
  geoRadiusM: number;
  gpsToleranceM: number;
  today: string;
  dataAsOf: string | null;
  hasLinko: boolean;
  activeVisit: FieldVisitRow | null;
};

export type FieldAmount = { sum: number; kg: number; orders: number };

export type FieldPlanFact = {
  planKg: number | null;
  planSum: number | null;
  factKg: number;
  factSum: number;
  shareKg: number | null;
  shareSum: number | null;
  expected: number;
  forecastKg: number | null;
  forecastSum: number | null;
};

export type FieldDayPoint = { date: string; sum: number; kg: number; orders: number };

export type FieldRoutePoint = {
  id: string;
  sequence: number;
  marketId: number;
  name: string;
  address: string | null;
  lat: number | null;
  lon: number | null;
  plannedTime: string | null;
  status: FieldPointStatus;
  linkoDone: boolean;
  visitId: string | null;
  arrival: string | null;
  departure: string | null;
  note: string | null;
  priority: FieldPriority;
  openTasks: number;
};

export type FieldRoute = {
  id: string;
  date: string;
  agentId: string;
  agentName: string;
  status: FieldRouteStatus;
  source: FieldRouteSource;
  distanceKm: number;
  estimatedMinutes: number;
  planned: number;
  visited: number;
  skipped: number;
  remaining: number;
  nextPointId: string | null;
  canEdit: boolean;
  points: FieldRoutePoint[];
};

export type FieldRouteSummary = {
  id: string;
  date: string;
  agentId: string;
  agentName: string;
  teamName: string | null;
  status: FieldRouteStatus;
  source: FieldRouteSource;
  planned: number;
  visited: number;
  skipped: number;
  distanceKm: number;
  estimatedMinutes: number;
};

export type FieldTask = {
  id: string;
  title: string;
  description: string | null;
  priority: FieldPriority;
  status: FieldTaskStatus;
  dueDate: string | null;
  overdue: boolean;
  assignedToId: string;
  assignedTo: string;
  createdByType: FieldActorType;
  createdBy: string | null;
  marketId: number | null;
  marketName: string | null;
  createdAt: string;
  completedAt: string | null;
  result: string | null;
  recommendationId: string | null;
  canEdit: boolean;
  nextStatuses: FieldTaskStatus[];
};

export type FieldRecommendation = {
  id: string;
  kind: FieldRecommendationKind;
  title: string;
  reason: string;
  confidence: number;
  status: FieldRecommendationStatus;
  agentId: string | null;
  agentName: string | null;
  supervisorId: string | null;
  supervisorName: string | null;
  marketId: number | null;
  marketName: string | null;
  priority: FieldPriority;
  dueDate: string | null;
  createdAt: string;
  decidedBy: string | null;
  decidedAt: string | null;
  decisionNote: string | null;
  taskId: string | null;
  sourceData: Record<string, unknown>;
  canDecide: boolean;
};

export type FieldToday = {
  date: string;
  agentId: string;
  agentName: string;
  today: FieldAmount;
  dailyPlanKg: number | null;
  dailyPlanSum: number | null;
  dayShareKg: number | null;
  dayShareSum: number | null;
  month: FieldPlanFact;
  visitsToday: number;
  linkoVisitsDone: number;
  linkoVisitsPlanned: number;
  tasksOpen: number;
  tasksDueToday: number;
  tasksDoneToday: number;
  tasksOverdue: number;
  route: FieldRoute | null;
  activeVisit: FieldVisitRow | null;
  tasks: FieldTask[];
  recommendations: FieldRecommendation[];
  days: FieldDayPoint[];
  dataAsOf: string | null;
};

export type FieldAgentDayRow = {
  agentId: string;
  name: string;
  teamId: string | null;
  teamName: string | null;
  sumToday: number;
  kgToday: number;
  ordersToday: number;
  dailyPlanKg: number | null;
  dayShareKg: number | null;
  monthKg: number;
  monthSum: number;
  planKg: number | null;
  monthShareKg: number | null;
  behindPp: number | null;
  visits: number;
  routePlanned: number;
  routeVisited: number;
  routeSkipped: number;
  tasksOpen: number;
  tasksOverdue: number;
  tasksDoneToday: number;
  farVisits: number;
  status: AgentDayStatus;
  statusNote: string | null;
};

export type FieldTeamRow = {
  teamId: string;
  name: string;
  supervisor: string | null;
  agents: number;
  sumToday: number;
  monthKg: number;
  planKg: number | null;
  shareKg: number | null;
  visits: number;
  tasksOverdue: number;
  problems: number;
};

export type FieldAttention = { agentId: string; title: string; details: string[]; severity: "bad" | "warn"; link: string };

export type FieldDashboard = {
  date: string;
  role: FieldRole;
  agents: number;
  supervisors: number;
  activeAgents: number;
  today: FieldAmount;
  dailyPlanKg: number | null;
  dayShareKg: number | null;
  month: FieldPlanFact;
  visits: number;
  plannedPoints: number;
  visitedPoints: number;
  coverage: number | null;
  tasksOpen: number;
  tasksDoneToday: number;
  tasksOverdue: number;
  taskCompletion: number | null;
  activeRoutes: number;
  pendingRecommendations: number;
  agentRows: FieldAgentDayRow[];
  teams: FieldTeamRow[];
  attention: FieldAttention[];
  days: FieldDayPoint[];
  dataAsOf: string | null;
};

export type FieldAgentKpi = {
  agentId: string;
  name: string;
  teamId: string | null;
  teamName: string | null;
  sum: number;
  kg: number;
  planKg: number | null;
  planSum: number | null;
  shareKg: number | null;
  shareSum: number | null;
  forecastShare: number | null;
  orders: number;
  activeMarkets: number;
  planAkb: number | null;
  assignedMarkets: number;
  visitsDone: number;
  visitsPlanned: number;
  visitPlanShare: number | null;
  salesBaseVisits: number;
  tasksTotal: number;
  tasksDone: number;
  taskShare: number | null;
  routePoints: number;
  routeVisited: number;
  routeCoverage: number | null;
  marketCoverage: number | null;
  sumPerVisit: number | null;
};

export type FieldTeamKpi = {
  teamId: string;
  name: string;
  supervisor: string | null;
  agents: number;
  sum: number;
  kg: number;
  planKg: number | null;
  shareKg: number | null;
  avgAgentShare: number | null;
  visitsDone: number;
  tasksDone: number;
  tasksTotal: number;
  routeCoverage: number | null;
  marketCoverage: number | null;
  sumPerVisit: number | null;
};

export type FieldKpi = { year: number; month: number; asOf: string; expectedShare: number; total: FieldAgentKpi; agents: FieldAgentKpi[]; teams: FieldTeamKpi[] };

export type FieldMemberInfo = { id: string; userId: string | null; linkoUserId: number | null; role: FieldRole; fullName: string; phone: string | null; teamId: string | null; branchIds: number[]; isActive: boolean };

export type FieldAgentCard = { agent: FieldMemberInfo; teamName: string | null; supervisorName: string | null; today: FieldToday; kpi: FieldAgentKpi; visits: FieldVisitRow[]; canPlan: boolean };

export type FieldCustomerRow = {
  marketId: number;
  name: string;
  address: string | null;
  branch: string | null;
  type: string | null;
  lat: number | null;
  lon: number | null;
  agentId: string | null;
  agentName: string | null;
  priority: FieldPriority;
  status: FieldCustomerStatus;
  lastVisitDate: string | null;
  lastOrderDate: string | null;
  sales7: number;
  sales30: number;
  sales90: number;
  trendPct: number | null;
  monthlyTarget: number | null;
};

export type FieldVisitHistory = { source: string; at: string; agent: string | null; status: string; result: string | null; comment: string | null; distanceM: number | null; visitId: string | null };
export type FieldOrderRow = { id: number; date: string; acceptedDate: string | null; status: string; agent: string | null; sum: number; kg: number };
export type FieldMonthSales = { year: number; month: number; sum: number; kg: number; orders: number };
export type FieldAgentOption = { id: string; name: string; teamName: string | null };

export type FieldCustomerCard = {
  customer: FieldCustomerRow;
  linkoResponsibleId: number | null;
  linkoResponsibleName: string | null;
  supervisorId: string | null;
  supervisorName: string | null;
  contactPerson: string | null;
  phone: string | null;
  note: string | null;
  monthToDate: number;
  monthShare: number | null;
  orders30: number;
  visits30: number;
  months: FieldMonthSales[];
  orders: FieldOrderRow[];
  visits: FieldVisitHistory[];
  tasks: FieldTask[];
  recommendations: FieldRecommendation[];
  canEdit: boolean;
  canChangeAgent: boolean;
  agentOptions: FieldAgentOption[];
};

export type FieldMapPoint = { marketId: number; name: string; lat: number; lon: number; state: string; agentId: string | null; sequence: number | null; priority: FieldPriority; sales30: number };
export type FieldMapRoute = { routeId: string; agentId: string; agentName: string; marketIds: number[] };
export type FieldMapAgent = { agentId: string; name: string; lat: number; lon: number; at: string };
export type FieldMapView = { date: string; points: FieldMapPoint[]; routes: FieldMapRoute[]; agents: FieldMapAgent[]; truncated: boolean; total: number };

export type FieldMemberView = {
  id: string;
  fullName: string;
  role: FieldRole;
  phone: string | null;
  teamId: string | null;
  teamName: string | null;
  supervisorId: string | null;
  supervisorName: string | null;
  linkoUserId: number | null;
  linkoJob: string | null;
  linkoPosition: string | null;
  userId: string | null;
  login: string | null;
  isActive: boolean;
  branchIds: number[];
  markets: number;
  /** Можно менять карточку и вход (зона РМ; себя — нельзя). */
  editable: boolean;
};

export type FieldTeamView = { id: string; name: string; supervisorId: string | null; supervisorName: string | null; branchId: number | null; branchName: string | null; isActive: boolean; agents: number };
export type FieldCandidate = { linkoUserId: number; name: string; job: string | null; position: string | null; suggestedRole: FieldRole; branchId: number | null; branchName: string | null; markets: number };
export type FieldStructure = { members: FieldMemberView[]; teams: FieldTeamView[]; candidates: FieldCandidate[]; branches: { id: number; name: string }[]; canManage: boolean; canManageOrg: boolean };

export type FieldJointVisit = {
  id: string;
  date: string;
  time: string | null;
  marketId: number | null;
  marketName: string | null;
  objective: string;
  result: string | null;
  comment: string | null;
  status: "Planned" | "Done" | "Cancelled";
  participants: { memberId: string; name: string; role: FieldRole }[];
  canEdit: boolean;
};

export type FieldSettings = {
  id: number;
  geoRadiusM: number;
  gpsToleranceM: number;
  maxRoutePoints: number;
  visitMinutes: number;
  travelSpeedKmh: number;
  dayStart: string;
  notVisitedDays: number;
  declinePct: number;
  declineMinSales: number;
  behindPlanPct: number;
  autoPlanning: boolean;
  updatedAt: string;
};

export type FieldNotification = { id: string; kind: string; title: string; body: string | null; link: string | null; createdAt: string; read: boolean };
