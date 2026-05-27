import { contextBridge } from 'electron';

const versions = Object.freeze({
	node: process.versions.node,
	chrome: process.versions.chrome,
	electron: process.versions.electron,
});

contextBridge.exposeInMainWorld('versions', versions);
