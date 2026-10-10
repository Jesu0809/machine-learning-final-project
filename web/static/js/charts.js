const CHART_TEXT = "#c8ccd6";
const CHART_GRID = "rgba(255, 255, 255, 0.08)";
const POLICY_COLORS = { trained: "#e0313b", scripted: "#f2a33a", random: "#6c7489" };

Chart.defaults.color = CHART_TEXT;
Chart.defaults.borderColor = CHART_GRID;
Chart.defaults.maintainAspectRatio = false;

function lineChart(canvasId, steps, values, title) {
  new Chart(document.getElementById(canvasId), {
    type: "line",
    data: {
      labels: steps,
      datasets: [{
        label: title,
        data: values,
        borderColor: POLICY_COLORS.trained,
        borderWidth: 2,
        pointRadius: 0,
        tension: 0.2,
      }],
    },
    options: {
      plugins: { title: { display: true, text: title }, legend: { display: false } },
      scales: { x: { title: { display: true, text: "Training step" } } },
    },
  });
}

function successChart(canvasId, runs, presets, policyLabels) {
  const datasets = Object.keys(policyLabels).map((policy) => ({
    label: policyLabels[policy],
    backgroundColor: POLICY_COLORS[policy],
    data: presets.map((preset) => {
      const run = runs.find((r) => r.policy === policy && r.bot_preset === preset);
      return run ? run.success_rate * 100 : null;
    }),
  }));

  new Chart(document.getElementById(canvasId), {
    type: "bar",
    data: { labels: presets, datasets },
    options: {
      plugins: { title: { display: true, text: "Success rate by guard difficulty (%)" } },
      scales: { y: { min: 0, max: 100 } },
    },
  });
}
