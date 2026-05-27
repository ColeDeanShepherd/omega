import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

import type {
  AdoGitPullRequest,
  PullRequestStatus,
  ReviewerVote,
} from '../plugins/omega/pull-requests';

const execFileAsync = promisify(execFile);

type AzRepoPullRequestReviewer = Readonly<{
  displayName?: string;
  uniqueName?: string;
  vote?: number;
}>;

type AzRepoPullRequest = Readonly<{
  pullRequestId?: number;
  title?: string;
  repository?: {
    name?: string;
  };
  status?: PullRequestStatus;
  sourceRefName?: string;
  targetRefName?: string;
  createdBy?: {
    displayName?: string;
    uniqueName?: string;
  };
  isDraft?: boolean;
  creationDate?: string;
  closedDate?: string;
  reviewers?: ReadonlyArray<AzRepoPullRequestReviewer>;
}>;

const normalizeBranch = (refName: string | undefined): string =>
  (refName ?? '').replace('refs/heads/', '');

const mapVote = (vote: number | undefined): ReviewerVote => {
  if ((vote ?? 0) > 0) {
    return 'approved';
  }

  if ((vote ?? 0) <= -10) {
    return 'rejected';
  }

  return 'waiting';
};

const mapReviewers = (
  reviewers: ReadonlyArray<AzRepoPullRequestReviewer> | undefined,
): AdoGitPullRequest['reviewers'] =>
  (reviewers ?? []).map((reviewer) => ({
    displayName: reviewer.displayName ?? reviewer.uniqueName ?? 'Unknown reviewer',
    vote: mapVote(reviewer.vote),
  }));

const toAppPullRequest = (pullRequest: AzRepoPullRequest): AdoGitPullRequest => ({
  id: pullRequest.pullRequestId ?? 0,
  title: pullRequest.title ?? '(untitled)',
  repository: pullRequest.repository?.name ?? 'unknown-repo',
  status: pullRequest.status ?? 'active',
  sourceBranch: normalizeBranch(pullRequest.sourceRefName),
  targetBranch: normalizeBranch(pullRequest.targetRefName),
  author: pullRequest.createdBy?.displayName ?? pullRequest.createdBy?.uniqueName ?? 'Unknown author',
  isDraft: pullRequest.isDraft ?? false,
  updatedAt:
    pullRequest.closedDate ??
    pullRequest.creationDate ??
    new Date().toISOString(),
  reviewers: mapReviewers(pullRequest.reviewers),
});

const runAzCli = async (args: ReadonlyArray<string>): Promise<string> => {
  try {
    const command = (process.platform === 'win32') ? 'cmd.exe' : 'az';
    const commandArgs = (process.platform === 'win32') ? ['/d', '/s', '/c', 'az', ...args] : [...args];

    const { stdout } = await execFileAsync(command, commandArgs, {
      maxBuffer: 10 * 1024 * 1024,
    });

    return stdout;
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    throw new Error(`Azure CLI command failed: az ${args.join(' ')}\n${message}`);
  }
};

const runAzCliJson = async (args: ReadonlyArray<string>): Promise<unknown> => {
  const output = await runAzCli([...args, '-o', 'json']);

  try {
    return JSON.parse(output) as unknown;
  } catch {
    return { raw: output } as const;
  }
};

type JsonRecord = Record<string, unknown>;

const isJsonRecord = (value: unknown): value is JsonRecord =>
  typeof value === 'object' && value !== null;

const readString = (value: unknown): string | undefined =>
  typeof value === 'string' ? value : undefined;

const readNumber = (value: unknown): number | undefined =>
  typeof value === 'number' ? value : undefined;

const readBoolean = (value: unknown): boolean | undefined =>
  typeof value === 'boolean' ? value : undefined;

const readRecord = (value: unknown): JsonRecord | undefined =>
  isJsonRecord(value) ? value : undefined;

const normalizeListResponse = <T>(
  payload: unknown,
  mapper: (item: JsonRecord) => T,
): ReadonlyArray<T> => {
  if (Array.isArray(payload)) {
    return payload
      .map(readRecord)
      .filter((item): item is JsonRecord => item !== undefined)
      .map(mapper);
  }

  const asRecord = readRecord(payload);
  if (!asRecord || !Array.isArray(asRecord.value)) {
    return [];
  }

  return asRecord.value
    .map(readRecord)
    .filter((item): item is JsonRecord => item !== undefined)
    .map(mapper);
};

export type AdoPolicyEvaluationStatus =
  | 'queued'
  | 'running'
  | 'approved'
  | 'rejected'
  | 'notApplicable'
  | 'broken'
  | string;

export type AdoPrPolicyEvaluation = Readonly<{
  evaluationId?: string;
  artifactId?: string;
  status: AdoPolicyEvaluationStatus;
  policyTypeId?: string;
  policyTypeDisplayName?: string;
  configurationId?: number;
  isBlocking: boolean;
  isEnabled: boolean;
  context: Readonly<JsonRecord>;
}>;

export type AdoBuildSummary = Readonly<{
  id: number;
  buildNumber?: string;
  definitionId?: number;
  definitionName?: string;
  sourceBranch?: string;
  reason?: string;
  status?: string;
  result?: string;
  queueTime?: string;
  startTime?: string;
  finishTime?: string;
}>;

export type AdoTimelineIssue = Readonly<{
  type?: string;
  category?: string;
  message?: string;
}>;

export type AdoBuildLogReference = Readonly<{
  id?: number;
  type?: string;
  url?: string;
}>;

export type AdoTimelineRecord = Readonly<{
  id?: string;
  parentId?: string;
  type?: string;
  name?: string;
  state?: string;
  result?: string;
  errorCount?: number;
  warningCount?: number;
  log?: AdoBuildLogReference;
  issues: ReadonlyArray<AdoTimelineIssue>;
}>;

export type AdoBuildTimelineResponse = Readonly<{
  id?: string;
  changeId?: number;
  url?: string;
  records: ReadonlyArray<AdoTimelineRecord>;
}>;

export type AdoBuildLogsResponse = Readonly<
  | {
      format: 'text';
      text: string;
    }
  | {
      format: 'json';
      json: unknown;
    }
>;

export type AdoPrPoliciesResponse = ReadonlyArray<AdoPrPolicyEvaluation>;
export type AdoPipelineBuildListResponse = ReadonlyArray<AdoBuildSummary>;

const toPolicyEvaluation = (item: JsonRecord): AdoPrPolicyEvaluation => {
  const configuration = readRecord(item.configuration);
  const policyType = readRecord(configuration?.type);

  return {
    evaluationId: readString(item.evaluationId),
    artifactId: readString(item.artifactId),
    status: (readString(item.status) ?? 'queued') as AdoPolicyEvaluationStatus,
    policyTypeId: readString(policyType?.id),
    policyTypeDisplayName: readString(policyType?.displayName),
    configurationId: readNumber(configuration?.id),
    isBlocking: readBoolean(configuration?.isBlocking) ?? false,
    isEnabled: readBoolean(configuration?.isEnabled) ?? false,
    context: readRecord(item.context) ?? {},
  };
};

const toBuildSummary = (item: JsonRecord): AdoBuildSummary => {
  const definition = readRecord(item.definition);

  return {
    id: readNumber(item.id) ?? 0,
    buildNumber: readString(item.buildNumber),
    definitionId: readNumber(definition?.id),
    definitionName: readString(definition?.name),
    sourceBranch: readString(item.sourceBranch),
    reason: readString(item.reason),
    status: readString(item.status),
    result: readString(item.result),
    queueTime: readString(item.queueTime),
    startTime: readString(item.startTime),
    finishTime: readString(item.finishTime),
  };
};

const toTimelineIssue = (issue: JsonRecord): AdoTimelineIssue => ({
  type: readString(issue.type),
  category: readString(issue.category),
  message: readString(issue.message),
});

const toTimelineRecord = (record: JsonRecord): AdoTimelineRecord => {
  const logRecord = readRecord(record.log);
  const issues = Array.isArray(record.issues)
    ? record.issues
      .map(readRecord)
      .filter((item): item is JsonRecord => item !== undefined)
      .map(toTimelineIssue)
    : [];

  return {
    id: readString(record.id),
    parentId: readString(record.parentId),
    type: readString(record.type),
    name: readString(record.name),
    state: readString(record.state),
    result: readString(record.result),
    errorCount: readNumber(record.errorCount),
    warningCount: readNumber(record.warningCount),
    log: logRecord
      ? {
          id: readNumber(logRecord.id),
          type: readString(logRecord.type),
          url: readString(logRecord.url),
        }
      : undefined,
    issues,
  };
};

const normalizeTimelineResponse = (payload: unknown): AdoBuildTimelineResponse => {
  const timeline = readRecord(payload);
  const records = Array.isArray(timeline?.records)
    ? timeline.records
      .map(readRecord)
      .filter((item): item is JsonRecord => item !== undefined)
      .map(toTimelineRecord)
    : [];

  return {
    id: readString(timeline?.id),
    changeId: readNumber(timeline?.changeId),
    url: readString(timeline?.url),
    records,
  };
};

const normalizeBuildLogResponse = (payload: unknown): AdoBuildLogsResponse => {
  if (typeof payload === 'string') {
    return {
      format: 'text',
      text: payload,
    };
  }

  const asRecord = readRecord(payload);
  if (asRecord && typeof asRecord.raw === 'string') {
    return {
      format: 'text',
      text: asRecord.raw,
    };
  }

  return {
    format: 'json',
    json: payload,
  };
};

export const loadAdoPrPolicies = async (
  organization: string,
  pullRequestId: number,
): Promise<AdoPrPoliciesResponse> => {
  const payload = await runAzCliJson([
    'repos',
    'pr',
    'policy',
    'list',
    '--id',
    String(pullRequestId),
    '--organization',
    organization,
  ]);

  return normalizeListResponse(payload, toPolicyEvaluation);
};

export const loadAdoPipelineBuilds = async (
  organization: string,
  project: string,
  definitionId: number,
  branch: string,
): Promise<AdoPipelineBuildListResponse> => {
  const payload = await runAzCliJson([
    'pipelines',
    'build',
    'list',
    '--organization',
    organization,
    '--project',
    project,
    '--definition-ids',
    String(definitionId),
    '--branch',
    branch,
    '--reason',
    'pullRequest',
  ]);

  return normalizeListResponse(payload, toBuildSummary);
};

export const loadAdoBuildTimeline = async (
  organization: string,
  project: string,
  buildId: number,
): Promise<AdoBuildTimelineResponse> => {
  const payload = await runAzCliJson([
    'devops',
    'invoke',
    '--area',
    'build',
    '--resource',
    'timeline',
    '--route-parameters',
    `project=${project}`,
    `buildId=${String(buildId)}`,
    '--organization',
    organization,
    '--api-version',
    '7.0',
  ]);

  return normalizeTimelineResponse(payload);
};

export const loadAdoBuildLog = async (
  organization: string,
  project: string,
  buildId: number,
  logId: number,
): Promise<AdoBuildLogsResponse> => {
  const payload = await runAzCliJson([
    'devops',
    'invoke',
    '--area',
    'build',
    '--resource',
    'logs',
    '--route-parameters',
    `project=${project}`,
    `buildId=${String(buildId)}`,
    `logId=${String(logId)}`,
    '--organization',
    organization,
    '--api-version',
    '7.0',
  ]);

  return normalizeBuildLogResponse(payload);
};

export type AdoPrStepLogRequest = Readonly<{
  organization: string;
  project: string;
  pullRequestId: number;
  definitionId: number;
  branch: string;
  requiredCheckName: string;
  stageName: string;
  stepName: string;
}>;

export type AdoPrStepLogResult = Readonly<{
  policy: AdoPrPolicyEvaluation;
  build: AdoBuildSummary;
  stage: AdoTimelineRecord;
  step: AdoTimelineRecord;
  log: AdoBuildLogsResponse;
}>;

const isRejectedPolicy = (policy: AdoPrPolicyEvaluation): boolean =>
  policy.status.toLowerCase() === 'rejected';

const policyMatchesCheckName = (
  policy: AdoPrPolicyEvaluation,
  requiredCheckName: string,
): boolean => {
  const needle = requiredCheckName.trim().toLowerCase();
  if (!needle) {
    return true;
  }

  if (policy.policyTypeDisplayName?.toLowerCase().includes(needle)) {
    return true;
  }

  const contextText = JSON.stringify(policy.context).toLowerCase();
  return contextText.includes(needle);
};

const sortBuildsNewestFirst = (builds: ReadonlyArray<AdoBuildSummary>): ReadonlyArray<AdoBuildSummary> =>
  [...builds].sort((left, right) => {
    const leftTime = Date.parse(left.finishTime ?? left.startTime ?? left.queueTime ?? '') || 0;
    const rightTime = Date.parse(right.finishTime ?? right.startTime ?? right.queueTime ?? '') || 0;
    return rightTime - leftTime;
  });

const collectDescendantIds = (
  records: ReadonlyArray<AdoTimelineRecord>,
  rootId: string,
): ReadonlySet<string> => {
  const descendants = new Set<string>([rootId]);
  let changed = true;

  while (changed) {
    changed = false;
    for (const record of records) {
      if (!record.id || !record.parentId) {
        continue;
      }

      if (descendants.has(record.parentId) && !descendants.has(record.id)) {
        descendants.add(record.id);
        changed = true;
      }
    }
  }

  return descendants;
};

const findStepRecord = (
  timeline: AdoBuildTimelineResponse,
  stage: AdoTimelineRecord,
  stepName: string,
): AdoTimelineRecord | undefined => {
  if (!stage.id) {
    return undefined;
  }

  const descendantIds = collectDescendantIds(timeline.records, stage.id);
  const candidates = timeline.records.filter((record) => {
    if (!record.name || !record.id) {
      return false;
    }

    return descendantIds.has(record.id) && record.name === stepName;
  });

  if (candidates.length === 0) {
    return undefined;
  }

  return candidates.find((record) => record.result === 'failed') ?? candidates[0];
};

const findBuildForStepLog = (builds: ReadonlyArray<AdoBuildSummary>): AdoBuildSummary | undefined => {
  const newest = sortBuildsNewestFirst(builds);
  return newest.find((build) => build.status === 'completed' && build.result === 'failed') ?? newest[0];
};

export const loadAdoPrStepLog = async (
  request: AdoPrStepLogRequest,
): Promise<AdoPrStepLogResult> => {
  await ensureAzureDevOpsExtension();

  const policies = await loadAdoPrPolicies(request.organization, request.pullRequestId);
  const matchingPolicy = policies.find((policy) =>
    isRejectedPolicy(policy) && policyMatchesCheckName(policy, request.requiredCheckName),
  );

  if (!matchingPolicy) {
    throw new Error(
      `No rejected policy matched check \"${request.requiredCheckName}\" for PR ${String(request.pullRequestId)}.`,
    );
  }

  const builds = await loadAdoPipelineBuilds(
    request.organization,
    request.project,
    request.definitionId,
    request.branch,
  );
  const build = findBuildForStepLog(builds);

  if (!build) {
    throw new Error(
      `No pipeline builds found for definition ${String(request.definitionId)} on branch ${request.branch}.`,
    );
  }

  const timeline = await loadAdoBuildTimeline(request.organization, request.project, build.id);
  const stage = timeline.records.find((record) => record.name === request.stageName);
  if (!stage) {
    throw new Error(`Stage \"${request.stageName}\" was not found in build ${String(build.id)} timeline.`);
  }

  const step = findStepRecord(timeline, stage, request.stepName);
  if (!step) {
    throw new Error(
      `Step \"${request.stepName}\" was not found under stage \"${request.stageName}\" in build ${String(build.id)}.`,
    );
  }

  const logId = step.log?.id;
  if (!logId) {
    throw new Error(
      `Step \"${request.stepName}\" in build ${String(build.id)} did not contain a log reference.`,
    );
  }

  const log = await loadAdoBuildLog(request.organization, request.project, build.id, logId);

  return {
    policy: matchingPolicy,
    build,
    stage,
    step,
    log,
  };
};

const ensureAzureDevOpsExtension = async (): Promise<void> => {
  try {
    await runAzCli(['extension', 'show', '--name', 'azure-devops', '-o', 'none']);
  } catch {
    await runAzCli(['extension', 'add', '--name', 'azure-devops', '--only-show-errors']);
  }
};

const getCurrentAzureCliUser = async (): Promise<string> => {
  const output = await runAzCli(['account', 'show', '--query', 'user.name', '-o', 'tsv']);
  return output.trim();
};

const loadProjectPullRequests = async (
  organization: string,
  creator: string,
  project: string,
): Promise<ReadonlyArray<AdoGitPullRequest>> => {
  const output = await runAzCli([
    'repos',
    'pr',
    'list',
    '--organization',
    organization,
    '--project',
    project,
    '--creator',
    creator,
    '--status',
    'active',
    '-o',
    'json',
  ]);

  const parsed = JSON.parse(output) as AzRepoPullRequest[];
  return parsed.map(toAppPullRequest);
};

export const loadAdoPullRequests = async (
  organization: string,
  projects: ReadonlyArray<string>,
): Promise<ReadonlyArray<AdoGitPullRequest>> => {
  await ensureAzureDevOpsExtension();
  const creator = await getCurrentAzureCliUser();
  const pullRequestsByProject = await Promise.all(
    projects.map((project) => loadProjectPullRequests(organization, creator, project)),
  );

  return pullRequestsByProject.flat();
};