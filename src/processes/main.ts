// Entry point of the Electron main process.

import { app, BrowserWindow, ipcMain } from 'electron';
import path from 'node:path';
import started from 'electron-squirrel-startup';

import { registeredPlugins } from '../plugins/registered-plugins';
import { RecurringTaskRunner } from '../recurring-task';

// eslint-disable-next-line import/no-unresolved
import { loadAdoPullRequests, loadAdoPrStepLog } from './ado-client.js';
import type { AdoPrStepLogRequest } from './ado-client.js';

// Handle creating/removing shortcuts on Windows when installing/uninstalling.
if (started) {
  app.quit();
}

const recurringTaskRunners: RecurringTaskRunner[] = [];

const createWindow = () => {
  // Create the browser window.
  const mainWindow = new BrowserWindow({
    width: 800,
    height: 600,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
    },
  });

  // and load the index.html of the app.
  if (MAIN_WINDOW_VITE_DEV_SERVER_URL) {
    mainWindow.loadURL(MAIN_WINDOW_VITE_DEV_SERVER_URL);
  } else {
    mainWindow.loadFile(
      path.join(__dirname, `../processes/renderer/${MAIN_WINDOW_VITE_NAME}/index.html`),
    );
  }

  mainWindow.webContents.once('did-finish-load', () => {
    //startApp(mainWindow.webContents);
  });
};

// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
// Some APIs can only be used after this event occurs.
app.on('ready', () =>
{
  registeredPlugins.forEach((plugin) => {
    plugin.recurringTasks?.forEach((task) => {
      const runner = new RecurringTaskRunner(task);
      runner.start();
      recurringTaskRunners.push(runner);
    });
  });
  
  ipcMain.handle('ado:loadPullRequests', (_, organization: string, projects: ReadonlyArray<string>) =>
    loadAdoPullRequests(organization, projects),
  );
  ipcMain.handle('ado:loadPrStepLog', (_, request: AdoPrStepLogRequest) =>
    loadAdoPrStepLog(request),
  );
  createWindow();
});

app.on('before-quit', () => {
  recurringTaskRunners.forEach((runner) => runner.stop());
  recurringTaskRunners.length = 0;
});

// Quit when all windows are closed, except on macOS. There, it's common
// for applications and their menu bar to stay active until the user quits
// explicitly with Cmd + Q.
app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', () => {
  // On OS X it's common to re-create a window in the app when the
  // dock icon is clicked and there are no other windows open.
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow();
  }
});

// In this file you can include the rest of your app's specific main process
// code. You can also put them in separate files and import them here.
