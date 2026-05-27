const _ = {
	text: (text) => document.createTextNode(text),
	p: (children) => {
		const p = document.createElement('p')
		p.append(...children)
		return p
	}
};

// Export the library to the global scope so it can be used in renderer.js.
window._ = _;
