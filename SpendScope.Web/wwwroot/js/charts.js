// Chart.js wrapper (spec 19.7). Configs are built in C#; the only JS-side additions are
// formatter callbacks, which cannot be serialized from .NET.
window.spendscopeCharts = {
  instances: {},

  render(canvasId, config) {
    this.destroy(canvasId);
    const canvas = document.getElementById(canvasId);
    if (!canvas || typeof Chart === "undefined") return false;

    const textColor = getComputedStyle(document.body).getPropertyValue("--mud-palette-text-secondary") || "#666";
    Chart.defaults.color = textColor.trim();

    if (config.compactTicks && config.options?.scales?.y) {
      const compact = new Intl.NumberFormat(undefined, { notation: "compact", maximumFractionDigits: 1 });
      config.options.scales.y.ticks = { ...(config.options.scales.y.ticks || {}), callback: v => compact.format(v) };
    }

    if (config.currency) {
      const money = new Intl.NumberFormat(undefined, { style: "currency", currency: config.currency });
      config.options = config.options || {};
      config.options.plugins = config.options.plugins || {};
      config.options.plugins.tooltip = {
        callbacks: {
          label: ctx => {
            const value = ctx.parsed?.y ?? ctx.parsed;
            const label = ctx.label ? ctx.label + ": " : "";
            return label + money.format(value);
          }
        }
      };
    }

    delete config.compactTicks;
    delete config.currency;
    this.instances[canvasId] = new Chart(canvas, config);
    return true;
  },

  destroy(canvasId) {
    const existing = this.instances[canvasId];
    if (existing) {
      existing.destroy();
      delete this.instances[canvasId];
    }
  }
};
