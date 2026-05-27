const appContainer = document.getElementById('app')

const _ = {
  text: (text) => document.createTextNode(text),
  p: (children) => {
    const p = document.createElement('p')
    p.append(...children)
    return p
  }
}

const render = (content) => {
  appContainer.replaceChildren(content)
}

const information = _.p([
  _.text(`This app is using Chrome (v${window.versions.chrome()}), Node.js (v${window.versions.node()}), and Electron (v${window.versions.electron()})`)
])

render(information)
