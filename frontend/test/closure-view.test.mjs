import assert from 'node:assert/strict';
import test from 'node:test';

import { visibleClosures } from '../src/closure-view.ts';

// Synthetic test fixture, never bundled into the dashboard.
const pending = { id: '4b10f7bf-b2ea-4915-a420-6849164da377', status: 'pending' };
const closed = { id: 'bed63bde-0bf5-41bc-b3d1-aeb13513bfa9', status: 'closed' };
const rows = [pending, closed];

test('selecting a status displays only matching closures without altering the total list', () => {
  assert.deepEqual(visibleClosures(rows, 'pending'), [pending]);
  assert.deepEqual(visibleClosures(rows, 'closed'), [closed]);
  assert.deepEqual(visibleClosures(rows, 'all'), rows);
});
