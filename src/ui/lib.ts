export const _ = {
  text: (text: string) => document.createTextNode(text),
  p: (children: Node[]) => {
    const p = document.createElement('p');
    p.append(...children);
    return p;
  },
};