// Preload script for the Electron application.

import { contextBridge, ipcRenderer } from 'electron';

const electronApi = Object.freeze({
});

contextBridge.exposeInMainWorld('electronApi', electronApi);
