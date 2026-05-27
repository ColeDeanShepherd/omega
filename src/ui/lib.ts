export const text = (value: string): Text => document.createTextNode(value);

export const p = (...children: Array<Node | string>): HTMLParagraphElement => {
  const element = document.createElement('p');
  element.append(...children);
  return element;
};