const appView = () =>
  _.p([
    _.text(
      `This app is using Chrome (v${window.versions.chrome()}), Node.js (v${window.versions.node()}), and Electron (v${window.versions.electron()})`
    )
  ])

// Export the app view to the global scope so it can be used in renderer.js.
window.appView = appView