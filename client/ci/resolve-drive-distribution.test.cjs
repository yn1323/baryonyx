const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const resolve = require('./resolve-drive-distribution.cjs');

function scenario(overrides = {}, artifacts) {
  const sha = 'a'.repeat(40);
  const run = {
    id: 10, status: 'in_progress', conclusion: null, run_attempt: 2,
    path: '.github/workflows/client-ci.yml', repository: { full_name: 'owner/repo' },
    head_repository: { full_name: 'owner/repo' }, event: 'pull_request',
    head_sha: sha, head_branch: 'feature', pull_requests: [{ number: 5 }],
    ...overrides,
  };
  const pr = { number: 5, state: 'open', head: { sha, repo: { full_name: 'owner/repo' } } };
  const context = {
    repo: { owner: 'owner', repo: 'repo' }, runId: 10,
  };
  const github = {
    rest: {
      actions: {
        getWorkflowRun: async args => {
          assert.equal(args.run_id, context.runId);
          return { data: run };
        },
        listWorkflowRunArtifacts: 'artifacts',
      },
      pulls: { get: async () => ({ data: pr }) },
    },
    paginate: async (_api, args) => {
      assert.equal(args.run_id, 10);
      return artifacts || [{ id: 100, name: 'client-android-apk-preview-2', expired: false }];
    },
  };
  const input = { artifactId: '100', environment: 'preview' };
  return { run, pr, context, github, input };
}

test('PR build distributes its exact Preview artifact while the calling CI is running', async () => {
  const { github, context, input } = scenario();
  assert.deepEqual(await resolve(github, context, input), {
    environment: 'preview', 'artifact-id': '100', 'run-id': '10',
    'head-sha': 'a'.repeat(40), 'pr-number': '5',
  });
});

for (const overrides of [
  { status: 'completed', conclusion: 'failure' }, { status: 'queued' },
  { path: '.github/workflows/other.yml' },
  { head_repository: { full_name: 'fork/repo' } },
  { repository: { full_name: 'other/repo' } },
  { event: 'push', head_branch: 'feature' },
  { event: 'workflow_dispatch', head_branch: 'feature' },
  { event: 'workflow_run' }, { pull_requests: [] },
]) {
  test(`untrusted or ineligible source is rejected: ${JSON.stringify(overrides)}`, async () => {
    const { github, context, input } = scenario(overrides);
    assert.equal(await resolve(github, context, input), null);
  });
}

test('closed or superseded PR is not distributed', async () => {
  for (const change of ['closed', 'superseded', 'fork']) {
    const { github, context, pr, input } = scenario();
    if (change === 'closed') pr.state = 'closed';
    else if (change === 'fork') pr.head.repo.full_name = 'fork/repo';
    else pr.head.sha = 'b'.repeat(40);
    assert.equal(await resolve(github, context, input), null);
  }
});

test('PR artifact cannot select Prod', async () => {
  const { github, context, input } = scenario({}, [{ id: 100, name: 'client-android-apk-prod-2' }]);
  await assert.rejects(resolve(github, context, input), /environment does not match/);
  input.environment = 'prod';
  await assert.rejects(resolve(github, context, input), /environment does not match/);
});

for (const [branch, environment] of [['main', 'prod'], ['dev', 'dev'], ['develop', 'dev']]) {
  test(`push to ${branch} distributes ${environment}`, async () => {
    const { github, context, input } = scenario({ event: 'push', head_branch: branch }, [
      { id: 100, name: `client-android-apk-${environment}-2` },
    ]);
    input.environment = environment;
    assert.equal((await resolve(github, context, input)).environment, environment);
    input.environment = 'preview';
    await assert.rejects(resolve(github, context, input), /environment does not match/);
  });
}

test('manual runs on main and development branches can select each environment', async () => {
  for (const branch of ['main', 'dev', 'develop']) {
    for (const environment of ['dev', 'prod', 'preview']) {
      const { github, context, input } = scenario({ event: 'workflow_dispatch', head_branch: branch }, [
        { id: 100, name: `client-android-apk-${environment}-2` },
      ]);
      input.environment = environment;
      assert.equal((await resolve(github, context, input)).environment, environment);
    }
  }
});

test('missing, expired, duplicate and non-APK build artifacts are rejected', async () => {
  for (const artifacts of [
    [], [{ id: 101, name: 'client-android-apk-preview-2' }],
    [{ id: 100, name: 'client-tests-playmode' }],
    [{ id: 100, name: 'client-android-apk-preview-2', expired: true }],
    [{ id: 100, name: 'client-android-apk-preview-2' }, { id: 100, name: 'client-android-apk-preview-2' }],
  ]) {
    const { github, context, input } = scenario({}, artifacts);
    await assert.rejects(resolve(github, context, input), /exactly one/);
  }
});

test('retrying only distribution reuses the successful build ID, not another attempt', async () => {
  const { github, context, input } = scenario({ run_attempt: 3 }, [
    { id: 90, name: 'client-android-apk-preview-1' },
    { id: 100, name: 'client-android-apk-preview-2' },
    { id: 110, name: 'client-android-apk-preview-3' },
  ]);
  assert.equal((await resolve(github, context, input))['artifact-id'], '100');
});

test('invalid artifact IDs and unknown environments fail before artifact lookup', async () => {
  for (const invalid of [{ artifactId: '' }, { artifactId: '-1' }, { artifactId: 'NaN' },
    { artifactId: '9007199254740992' }, { environment: 'other' }]) {
    const { github, context, input } = scenario();
    github.paginate = async () => assert.fail('Artifacts must not be queried for invalid input.');
    await assert.rejects(resolve(github, context, { ...input, ...invalid }), /Invalid APK artifact ID|environment does not match/);
  }
});

test('the successful build calls distribution with its artifact ID and repository secrets', () => {
  const root = path.resolve(__dirname, '../..');
  const ci = fs.readFileSync(path.join(root, '.github/workflows/client-ci.yml'), 'utf8');
  const distribution = fs.readFileSync(path.join(root, '.github/workflows/client-distribute.yml'), 'utf8');
  assert.match(ci, /needs: android-build/);
  assert.match(ci, /uses: \.\/\.github\/workflows\/client-distribute\.yml/);
  assert.match(ci, /artifact-id: \$\{\{ needs\.android-build\.outputs\.artifact-id \}\}/);
  for (const secret of ['CLIENT_ID', 'CLIENT_SECRET', 'REFRESH_TOKEN']) {
    assert.ok(ci.includes(`GOOGLE_DRIVE_${secret}: \${{ secrets.GOOGLE_DRIVE_${secret} }}`));
  }
  assert.match(ci, /github\.event\.pull_request\.head\.repo\.full_name == github\.repository/);
  assert.match(distribution, /workflow_call:/);
  assert.doesNotMatch(distribution, /workflow_run:|environment: client-distribution/);
  assert.match(distribution, /ref: \$\{\{ github\.sha \}\}/);
  assert.match(ci, /queue: max/);
  assert.match(ci, /cancel-in-progress: false/);
  assert.match(distribution, /queue: max/);
  assert.match(distribution, /cancel-in-progress: false/);
  assert.match(distribution, /python3 client\/ci\/upload-drive-apk\.py/);
  assert.match(distribution, /path: \$\{\{ runner\.temp \}\}\/drive-artifact/);
});
