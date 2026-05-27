export type PullRequestStatus = 'active' | 'completed' | 'abandoned';
export type ReviewerVote = 'approved' | 'waiting' | 'rejected';

export type PullRequestReviewer = Readonly<{
  displayName: string;
  vote: ReviewerVote;
}>;

export type AdoGitPullRequest = Readonly<{
  id: number;
  title: string;
  repository: string;
  status: PullRequestStatus;
  sourceBranch: string;
  targetBranch: string;
  author: string;
  isDraft: boolean;
  updatedAt: string;
  reviewers: ReadonlyArray<PullRequestReviewer>;
}>;

export const mockAdoPullRequests: ReadonlyArray<AdoGitPullRequest> = [
  {
    id: 4121,
    title: 'Add feature flags for tenant-level rollout',
    repository: 'omega-api',
    status: 'active',
    sourceBranch: 'feature/tenant-rollout-flags',
    targetBranch: 'main',
    author: 'Priya Nair',
    isDraft: false,
    updatedAt: '2026-05-25T14:22:00Z',
    reviewers: [
      { displayName: 'Evan King', vote: 'approved' },
      { displayName: 'Maya Chen', vote: 'approved' },
      { displayName: 'Jon Park', vote: 'waiting' },
    ],
  },
  {
    id: 4118,
    title: 'Refactor PR sync worker retry policy',
    repository: 'omega-worker',
    status: 'active',
    sourceBranch: 'refactor/pr-sync-retry-policy',
    targetBranch: 'main',
    author: 'Diego Torres',
    isDraft: true,
    updatedAt: '2026-05-26T08:10:00Z',
    reviewers: [
      { displayName: 'Anna Lee', vote: 'waiting' },
      { displayName: 'Cole Shepherd', vote: 'waiting' },
    ],
  },
  {
    id: 4107,
    title: 'Improve markdown rendering in PR description panel',
    repository: 'omega-web',
    status: 'completed',
    sourceBranch: 'improvement/pr-markdown-rendering',
    targetBranch: 'main',
    author: 'Lena Schmidt',
    isDraft: false,
    updatedAt: '2026-05-23T17:40:00Z',
    reviewers: [
      { displayName: 'Sam Patel', vote: 'approved' },
      { displayName: 'Hector Ruiz', vote: 'approved' },
    ],
  },
  {
    id: 4093,
    title: 'Remove legacy branch filters from PR dashboard endpoint',
    repository: 'omega-api',
    status: 'abandoned',
    sourceBranch: 'cleanup/legacy-branch-filters',
    targetBranch: 'main',
    author: 'Nora White',
    isDraft: false,
    updatedAt: '2026-05-20T11:03:00Z',
    reviewers: [
      { displayName: 'Ibrahim Khan', vote: 'rejected' },
      { displayName: 'Jon Park', vote: 'waiting' },
    ],
  },
];

export const loadFakePullRequests = async (): Promise<ReadonlyArray<AdoGitPullRequest>> =>
  Promise.resolve(mockAdoPullRequests);

export const loadPullRequests = async (
  organization: string,
  projects: ReadonlyArray<string>,
): Promise<ReadonlyArray<AdoGitPullRequest>> => {
  if (window.electronApi?.loadPullRequests) {
    return window.electronApi.loadPullRequests(organization, projects);
  }

  return Promise.resolve(mockAdoPullRequests);
};
