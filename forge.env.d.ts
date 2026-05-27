/// <reference types="@electron-forge/plugin-vite/forge-vite-env" />

import type {
	AdoGitPullRequest,
	AdoPrStepLogRequest,
	AdoPrStepLogResult,
} from './src/plugins/omega/pull-requests';

interface ElectronApi {
	loadPullRequests: (
		organization: string,
		projects: ReadonlyArray<string>,
	) => Promise<ReadonlyArray<AdoGitPullRequest>>;
	loadPrStepLog: (
		request: AdoPrStepLogRequest,
	) => Promise<AdoPrStepLogResult>;
}

declare global {
	interface Window {
		electronApi: ElectronApi;
	}
}

export {};
