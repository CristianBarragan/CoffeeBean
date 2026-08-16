(() => {
  const root = document.querySelector('[data-pipeline]');
  if (!root) return;

  const dataUrl = root.dataset.pipeline;
  const flow = root.dataset.flow || 'json-read';
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  let data;
  let activeSample;
  let activeStep = 'caller';

  const escapeHtml = (value) => String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');

  const pretty = (value) => JSON.stringify(value, null, 2);

  const render = () => {
    const sample = activeSample;
    const step = sample.steps[activeStep];
    const stepIndex = data.steps.findIndex(s => s.id === activeStep);

    root.querySelector('[data-sample-eyebrow]').textContent = sample.eyebrow;
    root.querySelector('[data-sample-title]').textContent = sample.title;
    root.querySelector('[data-sample-description]').textContent = sample.description;

    root.querySelector('[data-flow]').innerHTML = data.steps.map((item, index) => `
      <button class="pipeline-node ${item.id === activeStep ? 'is-active' : ''} ${index < stepIndex ? 'is-complete' : ''}"
              type="button" data-step="${item.id}" aria-current="${item.id === activeStep ? 'step' : 'false'}">
        <span class="pipeline-node-dot" aria-hidden="true"></span>
        <span>${escapeHtml(sample.diagram[index] || item.label)}</span>
      </button>
      ${index < data.steps.length - 1 ? '<span class="pipeline-line" aria-hidden="true"></span>' : ''}
    `).join('');

    root.querySelector('[data-step-kicker]').textContent = `Phase ${stepIndex + 1} of ${data.steps.length}`;
    root.querySelector('[data-step-title]').textContent = step.title;
    root.querySelector('[data-step-body]').textContent = step.body;
    root.querySelector('[data-input-label]').textContent = step.inputLabel;
    root.querySelector('[data-output-label]').textContent = step.outputLabel;
    root.querySelector('[data-input]').textContent = pretty(step.input);
    root.querySelector('[data-output]').textContent = pretty(step.output);

    root.querySelector('[data-prev]').disabled = stepIndex === 0;
    root.querySelector('[data-next]').disabled = stepIndex === data.steps.length - 1;
    root.querySelector('[data-progress]').style.width = `${((stepIndex + 1) / data.steps.length) * 100}%`;

    root.querySelectorAll('[data-step]').forEach(button => {
      button.addEventListener('click', () => selectStep(button.dataset.step));
    });
  };

  const selectStep = (stepId) => {
    if (!activeSample.steps[stepId]) return;
    activeStep = stepId;
    render();
    if (!prefersReducedMotion) {
      root.querySelector('[data-detail]').animate(
        [{ opacity: 0.55, transform: 'translateY(5px)' }, { opacity: 1, transform: 'translateY(0)' }],
        { duration: 180, easing: 'ease-out' }
      );
    }
  };

  const setSample = (sampleId) => {
    const sample = data.samples.find(item => item.id === sampleId);
    if (!sample) return;
    activeSample = sample;
    activeStep = 'caller';
    root.querySelectorAll('[data-sample]').forEach(button => {
      const selected = button.dataset.sample === sampleId;
      button.classList.toggle('is-active', selected);
      button.setAttribute('aria-selected', selected ? 'true' : 'false');
    });
    render();
  };

  const boot = async () => {
    try {
      const response = await fetch(dataUrl, { headers: { Accept: 'application/json' } });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      data = await response.json();
      activeSample = data.samples.find(item => item.id === flow) || data.samples[0];

      root.querySelector('[data-sample-switcher]').innerHTML = data.samples.map(sample => `
        <button type="button" class="sample-tab ${sample.id === activeSample.id ? 'is-active' : ''}"
                data-sample="${sample.id}" role="tab" aria-selected="${sample.id === activeSample.id ? 'true' : 'false'}">
          <span class="sample-tab-number">${sample.id === 'json-read' ? '01' : '02'}</span>
          <span>${escapeHtml(sample.title)}</span>
        </button>
      `).join('');
      root.querySelectorAll('[data-sample]').forEach(button => {
        button.addEventListener('click', () => setSample(button.dataset.sample));
      });

      root.querySelector('[data-prev]').addEventListener('click', () => {
        const index = data.steps.findIndex(s => s.id === activeStep);
        if (index > 0) selectStep(data.steps[index - 1].id);
      });
      root.querySelector('[data-next]').addEventListener('click', () => {
        const index = data.steps.findIndex(s => s.id === activeStep);
        if (index < data.steps.length - 1) selectStep(data.steps[index + 1].id);
      });

      render();
      root.classList.add('is-ready');
    } catch (error) {
      root.innerHTML = `<div class="pipeline-error"><strong>Pipeline demo could not load.</strong><span>Serve the docs-site over HTTP so the step payload can be loaded with fetch().</span><code>${escapeHtml(error.message)}</code></div>`;
    }
  };

  boot();
})();
