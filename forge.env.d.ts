/// <reference types="@electron-forge/plugin-vite/forge-vite-env" />

import type { AdoGitPullRequest } from './src/plugins/omega/pull-requests';

interface ElectronApi {
	loadPullRequests: (
		organization: string,
		projects: ReadonlyArray<string>,
	) => Promise<ReadonlyArray<AdoGitPullRequest>>;
}

declare global {
	interface Window {
		electronApi: ElectronApi;
	}
}

export {};
