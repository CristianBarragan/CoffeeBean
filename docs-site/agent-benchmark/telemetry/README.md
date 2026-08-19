# Live benchmark telemetry adapter

The benchmark page intentionally separates **measured benchmark results** from **live impact telemetry**. It must never turn a token percentage into a claimed electricity or carbon percentage.

## Endpoint used by the page

`GET /api/benchmark-impact?test=10000-c64`

Expected response:

```json
{
  "status": "live",
  "capturedAt": "2026-08-19T00:00:00Z",
  "test": "10000-c64",
  "conventional": {
    "powerW": 42.1,
    "energyWh": 0.117,
    "inputTokens": 1620,
    "outputTokens": 208,
    "totalTokens": 1828,
    "carbonIntensityGPerKwh": 102.4,
    "co2G": 0.0120
  },
  "foundgine": {
    "powerW": 35.7,
    "energyWh": 0.099,
    "inputTokens": 210,
    "outputTokens": 205,
    "totalTokens": 415,
    "carbonIntensityGPerKwh": 102.4,
    "co2G": 0.0101
  }
}
```

The numbers above are **schema examples only** and must not be published as benchmark results. The UI shows `—` until the endpoint supplies real telemetry.

## Power / energy

Scaphandre can expose host power and per-process power through its Prometheus exporter, including `scaph_process_power_consumption_microwatts`. Prefer process/service-level measurements when the benchmark is isolated; otherwise record the host measurement and disclose that scope.

## Grid carbon intensity

Electricity Maps provides current and historical carbon-intensity data in gCO2eq/kWh. The adapter should request the zone corresponding to the benchmark execution host and store the timestamp and emission-factor type with the measurement.

## Model token usage

For live agent runs, capture the provider's actual response usage fields. For example, OpenAI Responses exposes `input_tokens`, `output_tokens`, and `total_tokens` in the response usage object. Never estimate provider tokens when actual usage is available.

## Calculation

For each implementation:

`energyWh = measured power/energy integration over the benchmark interval`

`CO2g = energyWh / 1000 × carbonIntensityGPerKwh`

`token cost = provider-reported token counts × the published model pricing for that run`

The benchmark UI should report the measurement scope and timestamp alongside each live value.
