const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const ts = require('typescript');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../src/app/pages/profile/watch-time.utils.ts'), 'utf8');
const context = { exports: {} };
vm.runInNewContext(ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText, context);
const { formatWatchTime, describeMinutes, watchTimeBarHeight } = context.exports;
const formatted = value => JSON.parse(JSON.stringify(formatWatchTime(value)));

for (const [minutes, expected] of [
  [0, [{ value: 0, unit: 'minutos' }]],
  [1, [{ value: 1, unit: 'minuto' }]],
  [24, [{ value: 24, unit: 'minutos' }]],
  [59, [{ value: 59, unit: 'minutos' }]],
  [60, [{ value: 1, unit: 'hora' }, { value: 0, unit: 'minutos' }]],
  [744, [{ value: 12, unit: 'horas' }, { value: 24, unit: 'minutos' }]],
  [43200, [{ value: 1, unit: 'mês' }, { value: 0, unit: 'horas' }]],
  [3 * 43200 + 17 * 60, [{ value: 3, unit: 'meses' }, { value: 17, unit: 'horas' }]],
  [-1, [{ value: 0, unit: 'minutos' }]],
]) assert.deepEqual(formatted(minutes), expected, `Incorrect format for ${minutes} minutes`);

assert.equal(describeMinutes(0), '0 min');
assert.equal(describeMinutes(59), '59 min');
assert.equal(describeMinutes(60), '1 h');
assert.equal(describeMinutes(744), '12 h 24 min');
assert.equal(watchTimeBarHeight(0, 0), 3);
assert.equal(watchTimeBarHeight(30, 60), 45);
assert.equal(watchTimeBarHeight(120, 120), 90);
assert.equal(watchTimeBarHeight(1, 600), 4);
console.log('Watch time checks passed: display thresholds, singular/plural units, exact tooltip values and chart scaling.');
