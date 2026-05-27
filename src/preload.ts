// Preload script for the Electron application.

import { contextBridge, ipcRenderer } from 'electron';

import type {
	AdoGitPullRequest,
	AdoPrStepLogRequest,
	AdoPrStepLogResult,
} from './plugins/omega/pull-requests';

const electronApi = Object.freeze({
	loadPullRequests: (
		organization: string,
		projects: ReadonlyArray<string>,
	): Promise<ReadonlyArray<AdoGitPullRequest>> =>
		ipcRenderer.invoke('ado:loadPullRequests', organization, projects) as Promise<ReadonlyArray<AdoGitPullRequest>>,
	loadPrStepLog: (request: AdoPrStepLogRequest): Promise<AdoPrStepLogResult> =>
		ipcRenderer.invoke('ado:loadPrStepLog', request) as Promise<AdoPrStepLogResult>,
});

contextBridge.exposeInMainWorld('electronApi', electronApi);
