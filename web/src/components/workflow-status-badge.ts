export const workflowStatusBadgeTag = 'workflow-status-badge';

export class WorkflowStatusBadgeElement extends HTMLElement {
  static observedAttributes = ['status'];

  private readonly label: HTMLSpanElement;

  constructor() {
    super();
    const root = this.attachShadow({ mode: 'open' });
    const style = document.createElement('style');
    style.textContent = `
      :host { display: inline-block; }
      span {
        display: inline-block;
        min-width: 4.75rem;
        border-radius: 999px;
        padding: 0.32rem 0.62rem;
        text-align: center;
        font: 800 0.72rem/1.15 Inter, ui-sans-serif, system-ui, sans-serif;
      }
      .active { background: #e2f2e9; color: #195c3d; }
      .draft { background: #e6edf5; color: #2f577b; }
      .archived { background: #eceeec; color: #59635f; }
      .unknown { background: #fff2d9; color: #714611; }
    `;
    this.label = document.createElement('span');
    root.append(style, this.label);
  }

  attributeChangedCallback() {
    this.render();
  }

  connectedCallback() {
    this.render();
  }

  private render() {
    const status = this.getAttribute('status');
    const normalized =
      status === 'Draft' || status === 'Active' || status === 'Archived' ? status : 'Unknown';
    this.label.className = normalized.toLowerCase();
    this.label.textContent = normalized;
  }
}

if (typeof customElements !== 'undefined' && !customElements.get(workflowStatusBadgeTag)) {
  customElements.define(workflowStatusBadgeTag, WorkflowStatusBadgeElement);
}
