const appContainer = document.getElementById('app')

const render = (content) => {
  appContainer.replaceChildren(content)
}

render(window.appView())
