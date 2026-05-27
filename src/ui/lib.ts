export const text = (value: string): Text => document.createTextNode(value);

type Child = Node | string | number | null | undefined;
type TagName = keyof HTMLElementTagNameMap;

const appendChild = (element: HTMLElement, child: Child): void => {
  if (child === null || child === undefined) {
    return;
  }

  element.append(child instanceof Node ? child : String(child));
};

const elem = <T extends TagName>(tagName: T, ...children: Child[]): HTMLElementTagNameMap[T] => {
  const element = document.createElement(tagName);
  children.forEach((child) => appendChild(element, child));
  return element;
};

const tag = <T extends TagName>(tagName: T) => (...children: Child[]) => elem(tagName, ...children);

export const p = tag('p');
export const div = tag('div');
export const h2 = tag('h2');
export const ul = tag('ul');
export const li = tag('li');
export const small = tag('small');
export const strong = tag('strong');