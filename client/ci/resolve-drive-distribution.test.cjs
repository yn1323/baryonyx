const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const resolve = require('./resolve-drive-distribution.cjs');

function scenario(overrides = {}, artifacts) {
  const sha = 'a'.repeat(40);
  const run = {
    id: 10, status: 'completed', conclusion: 'success', run_attempt: 2,
    path: '.github/workflows/client-ci.yml', repository: { full_name: 'owner/repo' },
    head_repository: { full_name: 'owner/repo' }, event: 'pull_request',
    head_sha: sha, head_branch: 'feature', pull_requests: [{ number: 5 }],
    ...overrides,
  };
  const pr = { number: 5, state: 'open', head: { sha, repo: { full_name: 'owner/repo' } } };
  const context = {
    repo: { owner: 'owner', repo: 'repo' },
    payload: { workflow_run: { id: 10 }, repository: { default_branch: 'main' } },
  };
  const github = {
    rest: {
      actions: { getWorkflowRun: async () => ({ data: run }), listWorkflowRunArtifacts: 'artifacts' },
      pulls: { get: async () => ({ data: pr }) },
    },
    paginate: async (_api, args) => {
      assert.equal(args.run_id, 10);
      return artifacts || [{ id: 100, name: 'client-android-apk-preview-2', expired: false }];
    },
  };
  return { run, pr, context, github };
}

test('PR source is Preview and uses only its completed run artifact', async () => {
  const { github, context } = scenario();
  assert.deepEqual(await resolve(github, context), {
    environment: 'preview', 'artifact-id': '100', 'run-id': '10',
    'head-sha': 'a'.repeat(40), 'pr-number': '5',
  });
});

for (const overrides of [
  { conclusion: 'failure' }, { status: 'in_progress' },
  { path: '.github/workflows/other.yml' },
  { head_repository: { full_name: 'fork/repo' } },
  { repository: { full_name: 'other/repo' } },
  { event: 'push', head_branch: 'feature' },
  { event: 'workflow_dispatch', head_branch: 'feature' },
  { event: 'workflow_run' }, { pull_requests: [] },
]) {
  test(`untrusted or ineligible source is rejected: ${JSON.stringify(overrides)}`, async () => {
    const { github, context } = scenario(overrides);
    assert.equal(await resolve(github, context), null);
  });
}

test('closed or superseded PR is not distributed', async () => {
  for (const change of ['closed', 'superseded']) {
    const { github, context, pr } = scenario();
    if (change === 'closed') pr.state = 'closed';
    else pr.head.sha = 'b'.repeat(40);
    assert.equal(await resolve(github, context), null);
  }
});

test('PR artifact cannot select Prod', async () => {
  const { github, context } = scenario({}, [{ id: 100, name: 'client-android-apk-prod-2' }]);
  await assert.rejects(resolve(github, context), /environment does not match/);
});

test('push to main distributes Dev', async () => {
  const { github, context } = scenario({ event: 'push', head_branch: 'main' }, [
    { id: 100, name: 'client-android-apk-dev-2' },
  ]);
  assert.equal((await resolve(github, context)).environment, 'dev');
});

test('trusted manual run can select each environment', async () => {
  for (const environment of ['dev', 'prod', 'preview']) {
    const { github, context } = scenario({ event: 'workflow_dispatch', head_branch: 'main' }, [
      { id: 100, name: `client-android-apk-${environment}-2` },
    ]);
    assert.equal((await resolve(github, context)).environment, environment);
  }
});

test('expired, duplicate and old-attempt artifacts are rejected', async () => {
  for (const artifacts of [
    [], [{ id: 100, name: 'client-android-apk-preview-1' }],
    [{ id: 100, name: 'client-android-apk-preview-2', expired: true }],
    [{ id: 100, name: 'client-android-apk-preview-2' }, { id: 101, name: 'client-android-apk-dev-2' }],
  ]) {
    const { github, context } = scenario({}, artifacts);
    await assert.rejects(resolve(github, context), /exactly one/);
  }
});

test('PR CI has no Drive secrets and distribution runs trusted code with queued concurrency', () => {
  const root = path.resolve(__dirname, '../..');
  const ci = fs.readFileSync(path.join(root, '.github/workflows/client-ci.yml'), 'utf8');
  const distribution = fs.readFileSync(path.join(root, '.github/workflows/client-distribute.yml'), 'utf8');
  assert.doesNotMatch(ci, /secrets\.GOOGLE_DRIVE_/);
  assert.match(distribution, /workflow_run:/);
  assert.match(distribution, /ref: \$\{\{ github\.sha \}\}/);
  assert.doesNotMatch(distribution, /ref:.*(head_sha|head\.sha)/);
  assert.match(distribution, /environment: client-distribution/);
  assert.match(distribution, /queue: max/);
  assert.match(distribution, /cancel-in-progress: false/);
  assert.match(distribution, /python3 trusted\/client\/ci\/upload-drive-apk\.py/);
  assert.match(distribution, /path: \$\{\{ runner\.temp \}\}\/drive-artifact/);
});
